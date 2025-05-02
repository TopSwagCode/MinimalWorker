# GitHub Copilot Custom Instructions for MinimalWorker

MinimalWorker is a lightweight .NET library designed to simplify the registration and management of background workers in ASP.NET Core and .NET applications using the `IHost` interface.

## 🧠 Project Overview

- **Purpose**: Provide a minimal and clean API to register background tasks that run continuously or periodically.
- **Key Features**:
  - Easy registration of background workers with a single method call.
  - Support for periodic background tasks.
  - Integration with `CancellationToken` for graceful shutdowns.
  - Seamless support for dependency injection via `IServiceProvider`.

## 🛠️ Development Guidelines

- **Language**: C#
- **Framework**: .NET (compatible with ASP.NET Core)
- **Coding Standards**:
  - Use `async`/`await` for asynchronous operations.
  - Follow PascalCase for class and method names.
  - Use camelCase for local variables and parameters.
  - Include XML documentation comments for public APIs.

## 🔄 Background Worker Implementation

When implementing background workers:

- Inherit from `BackgroundService` or implement `IHostedService`.
- Override the `ExecuteAsync` method for long-running tasks.
- Utilize `CancellationToken` to handle graceful shutdowns.
- Register workers using the provided extension methods for clarity and consistency.

## 🧪 Testing

- Write unit tests for each background worker to ensure reliability.
- Use mocking frameworks to simulate dependencies.
- Test cancellation and exception handling scenarios.

## 📦 NuGet Package

- The library is published as a NuGet package for easy integration.
- Ensure versioning follows [Semantic Versioning](https://semver.org/) principles.

## 📚 Documentation

- Maintain up-to-date README with usage examples.
- Provide code snippets demonstrating common use cases.

---

By adhering to these guidelines, GitHub Copilot can better assist in generating code that aligns with the project's architecture and coding standards.
