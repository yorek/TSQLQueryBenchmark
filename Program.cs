using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using System.IO;
using System.Reflection;

namespace FTTS
{
    public class QueryStats
    {
        public int LogicalReads { get; set; }
        public int PhysicalReads { get; set; }
        public int ReadAheadReads { get; set; }
        public int ElapsedMs { get; set; }
        public int CpuMs { get; set; }

        public override string ToString()
        {
            return string.Format("Logical Reads:{0}, Physical Reads:{1}, Read-Ahead Reads:{2}, CpuMs Msec:{3}, Elapsed Msec:{4}", LogicalReads, PhysicalReads, ReadAheadReads, CpuMs, ElapsedMs);
        }
    }


    public class Program
    {
        private static List<string> messages = new List<string>();
        
        private static string[] allowedCommands = new string[] { "connectionstring", "query" };

        public static void Main(string[] args)
        {
            string connectionString = string.Empty;
            string commandFile = "CommandFile.txt";

            Console.WriteLine("T-SQL Query Benchmark");
            Console.WriteLine("(c) Davide Mauri 2019");
            Console.WriteLine("Beta Version, Use at your own risk!");
            Console.WriteLine("Version: " + Assembly.GetExecutingAssembly().GetName().Version.ToString());            

            switch (args.Length)
            {
                case 0: break;
                case 1: commandFile = args[1]; break;
                default:
                    Console.WriteLine("Unknown command line arguments. Use:");
                    Console.WriteLine("TQB [command file]");
                    return;
            }

            List<string> commandLines = new List<string>();
            try
            {
                Console.Write("Loading command line...");
                commandLines = LoadCommandFile(commandFile);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed.");
                Console.WriteLine("!!! Unable to parse command line.");
                Console.WriteLine("!!! " + e.Message);
                return;
            }
            Console.WriteLine("Done.");

            StreamWriter sw = null;

            foreach (string commandLine in commandLines)
            {
                Dictionary<string, string> command = DecodeCommandLine(commandLine);

                if (command["command"].ToLower() == "connectionstring")
                {
                    foreach (KeyValuePair<string, string> kvp in command)
                    {
                        if (kvp.Key != "command") connectionString += kvp.Key + "=" + kvp.Value + ";";
                    }

                    Console.Write("Testing connection string...");
                    SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString);
                    SqlConnection connTest = new SqlConnection(builder.ConnectionString);
                    try
                    {
                        connTest.Open();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Failed.");
                        Console.WriteLine("!!! Connection to DB cannot be opened.");
                        Console.WriteLine("!!! " + e.Message);
                        return;
                    }
                    finally
                    {
                        connTest.Close();
                    }
                    Console.WriteLine("Done.");

                    Console.WriteLine("Initializing output file...");
                    sw = File.CreateText(string.Format("TQB-TestResult-{0}-{1:yyyyMMddHHmm}.txt", builder.InitialCatalog, DateTime.Now));
                    sw.WriteLine("Type, Test, Maxdop, TestNum, TestMaxNum, LogicalReads, Physical, ReadAhead, CpuMs, ElapsedMs");            
                }

                if (command["command"].ToLower() == "query")
                {
                    string test = command["test"].ToLower();
                    string type = command["query"].ToLower();
                    string query = LoadQuery(type);
                    int cycle =  Convert.ToInt32(command["cycle"].ToLower());
                    int maxdop = Convert.ToInt32(command["maxdop"].ToLower());

                    for (int c = 1; c <= cycle; c++)
                    {
                        //Console.Write("{0}, {1}, {2}, {3}, {4}, ", type, test, maxdop, c, cycle);
                        Console.Write("Executing \"{0}\" test, {1} out of {2}, with \"{3}\" query and {4} maxdop...", test, c, cycle, type, maxdop);
                        QueryStats result = ExecuteTestQuery(test, connectionString, query, maxdop);
                        Console.WriteLine("Done. [{0} Logical I/O, {1}/{2} ms]", result.LogicalReads, result.CpuMs, result.ElapsedMs);

                        sw.WriteLine("{0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}", type, test, maxdop, c, cycle, result.LogicalReads, result.PhysicalReads, result.ReadAheadReads, result.CpuMs, result.ElapsedMs);
                        sw.Flush();
                    }
                }               
            }                  
            sw.Close();

            Console.WriteLine("Finished. Press any key to exit.");

            Console.ReadLine();
        }

        private static QueryStats ExecuteTestQuery(string test, string connectionString, string query, int maxdop)
        {
            QueryStats result = new QueryStats();
            
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();

            SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = "SET STATISTICS IO ON; SET STATISTICS TIME ON;";
            cmd.ExecuteNonQuery();

            cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT SERVERPROPERTY('Edition') AS Edition";
            var edition = (string)(cmd.ExecuteScalar());
                        
            if (test == "bcr" && edition != "SQL Azure")
            {
                cmd.CommandText = "DBCC DROPCLEANBUFFERS;";
                cmd.ExecuteNonQuery();
            }
        
            messages.Clear();
            conn.InfoMessage += new SqlInfoMessageEventHandler(InfoMessageHandler);

            cmd.CommandText = query + string.Format(" OPTION (RECOMPILE, MAXDOP {0})", maxdop);
            cmd.CommandTimeout = 60 * 60;
            cmd.ExecuteNonQuery();

            conn.Close();

            Regex reIO = null;
            Regex reTIME = new Regex(@"(?:CPU time = )(?<CPU>\d*).*(?:elapsed time = )(?<ELAPSED>\d*)");

            if (edition != "SQL Azure")
            {
                reIO = new Regex(@"(?:Table )'(?<TABLE>.*)'. (?:Scan count )(?<SCAN>\d*), (?:logical reads )(?<LOGICAL>\d*), (?:physical reads )(?<PHYSICAL>\d*), (?:read-ahead reads )(?<AHEAD>\d*)");
            } else {
                reIO = new Regex(@"(?:Table )'(?<TABLE>.*)'. (?:Scan count )(?<SCAN>\d*), (?:logical reads )(?<LOGICAL>\d*), (?:physical reads )(?<PHYSICAL>\d*), (?:page server reads )(?<PAGESERVER>\d*), (?:read-ahead reads )(?<AHEAD>\d*)");
            }

            bool ioStatFound = false;

            foreach (string message in messages)
            {
                MatchCollection mcIO = reIO.Matches(message);
                foreach (Match mIO in mcIO)
                {
                    if (mIO.Success)
                    {
                        result.LogicalReads += Convert.ToInt32(mIO.Groups["LOGICAL"].Value);
                        result.PhysicalReads += Convert.ToInt32(mIO.Groups["PHYSICAL"].Value);
                        result.ReadAheadReads += Convert.ToInt32(mIO.Groups["AHEAD"].Value);                        
                        ioStatFound = true;
                    }
                }

                if (ioStatFound == true)
                {
                    MatchCollection mcTIME = reTIME.Matches(message);
                    foreach (Match mTIME in mcTIME)
                    {
                        if (mTIME.Success)
                        {
                            result.CpuMs += Convert.ToInt32(mTIME.Groups["CPU"].Value);
                            result.ElapsedMs += Convert.ToInt32(mTIME.Groups["ELAPSED"].Value);
                        }
                    }
                }
            }

            return result;
        }

        private static string LoadQuery(string queryFile)
        {
            StreamReader sr = File.OpenText("queries/" + queryFile + ".qry");
            
            string query = sr.ReadToEnd();
            
            sr.Close();
            
            return query;
        }

        private static List<string> LoadCommandFile(string commandFile)
        {
            List<string> commands = new List<string>();

            StreamReader sr = File.OpenText(commandFile);
            while (true)
            {
                string command = sr.ReadLine().Trim();
                if (!command.StartsWith("//") && !string.IsNullOrEmpty(command))
                {
                    ValidateCommand(command);
                    commands.Add(command);
                }


                if (sr.EndOfStream) break;
            }
            sr.Close();

            return commands;
        }

        private static void InfoMessageHandler(object sender, SqlInfoMessageEventArgs e)
        {
            messages.Add(e.Message);
        }

        private static bool ValidateCommand(string command)
        {
            string[] sections = command.Split(';');

            if (sections.Length <= 1)
            {
                Console.WriteLine("!!! Command line not well formed.");
                return false;
            }

            foreach (string s in sections)
            {
                if (!string.IsNullOrEmpty(s))
                {
                    string[] keyvalue = s.Split('=');

                    string commandName = keyvalue[0].Trim().ToLower();
                    string commandValue = keyvalue[1].Trim().ToLower();

                    if (commandName == "command")
                    {

                        if (Array.IndexOf(allowedCommands, commandValue) < 0)
                        {
                            Console.WriteLine("!!! Command '{0}' unknown.", commandName);
                        }
                    }
                }
            }

            return true;
        }

        private static Dictionary<string, string> DecodeCommandLine(string commandLine)
        {
            Dictionary<string, string> commands = new Dictionary<string, string>();

            string[] sections = commandLine.Split(';');

            foreach (string s in sections)
            {
                if (!string.IsNullOrEmpty(s))
                {
                    string[] keyvalue = s.Split('=');

                    string commandName = keyvalue[0].Trim().ToLower();
                    string commandValue = keyvalue[1].Trim();

                    commands.Add(commandName, commandValue);
                }
            }

            return commands;
        }
    }
}


