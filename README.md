## Database Configuration

Update `appsettings.json` with your SQL Server connection string.

Example:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=BlogDb;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

Or using SQL Authentication:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=BlogDb;User Id=your_user;Password=your_password;TrustServerCertificate=True;"
}
```