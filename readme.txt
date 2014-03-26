T-SQL Query Benchmark

version 0.0.0.1

The project aim to help the testing and baselining of SQL Server systems. 
Through the definition of a 

CommandLine.txt 

file, it allows to execute test queries, measuring I/O e CPU performances.
Such metrics are currently read from the result of SET STATISTICS IO and SET STATISTICS TIME commands.

Tests can be run with:

cold cache (BCR: Base Consumption Rate) in order to measure and validate I/O performances 
warm cache (MCR: Maximum Consumption Rate) in order to measure and validate maximum system performances.

Defintion of BCR and MCR is taken from the Fast-Track SQL Server documentation:

http://www.microsoft.com/en-us/sqlserver/solutions-technologies/data-warehousing/reference-architecture.aspx 