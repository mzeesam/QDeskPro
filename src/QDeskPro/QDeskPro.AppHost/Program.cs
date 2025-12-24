var builder = DistributedApplication.CreateBuilder(args);

// Add SQL Server resource (uses existing local SQL Server)
// Note: In development, we use the existing local SQL Server instance
// For containerized development, uncomment the next line:
// var sql = builder.AddSqlServer("sql").AddDatabase("QDeskPro");

// Add the QDeskPro web application
var webApp = builder.AddProject<Projects.QDeskPro>("qdeskpro-web");

builder.Build().Run();
