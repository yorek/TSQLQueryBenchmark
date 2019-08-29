# T-SQL Query Benchmark

Version 0.0.0.2

The project aim to help the testing and baselining of SQL Server / Azure SQL databases.
Through the definition of a `CommandFile.txt` file, it allows to execute test queries, measuring I/O e CPU performances.
Such metrics are currently read from the result of SET STATISTICS IO and SET STATISTICS TIME commands.

Tests can be run with:

- cold cache (BCR: Base Consumption Rate) in order to measure and validate I/O performances (not available on Azure SQL)
- warm cache (MCR: Maximum Consumption Rate) in order to measure and validate maximum system performances.

Defintion of BCR and MCR is taken from the Fast-Track SQL Server documentation:

[Data Warehouse Fast Track for SQL Server 2016](https://www.jamesserra.com/archive/2016/10/data-warehouse-fast-track-for-sql-server-2016/)

to run the test, you need .NET Core 2.1. Then just copy the `CommandFile.sample.txt` to create your `CommandFile.txt` and then run

`dotnet run`

the queries references in `CommandFile.txt` must reside in the `queries` folder and have the `.qry` extension.
