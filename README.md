
## Design PeakMetrics Web App

This frontend has been migrated to an ASP.NET Core MVC implementation using:

- HTML5
- CSS3
- JavaScript
- Bootstrap 5
- Chart.js
- Razor Views (.cshtml)

## Run the Razor frontend

1. Ensure you have .NET SDK 8+ installed.
2. From the project root, run:

```bash
dotnet run
```

3. Open the local URL shown in the terminal (for example `https://localhost:xxxx`).

## Notes

- Razor pages are in `Views/Home`.
- Shared layout is in `Views/Shared/_Layout.cshtml`.
- Static CSS and JavaScript are in `wwwroot/css/site.css` and `wwwroot/js/site.js`.
- Legacy Vite/React source files and npm setup have been removed.

## C# Backend Ready Structure

The project is prepared for backend implementation in C# with these folders:

- `Models` for domain entities.
- `ViewModels` for page-specific data contracts.
- `Data` for EF Core DbContext and migrations.
- `Services` for business logic.
- `Repositories` for data-access abstractions/implementations.
  