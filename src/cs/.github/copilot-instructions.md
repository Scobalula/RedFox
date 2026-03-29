---
applyTo: "**/*.cs"
---
# C#/.NET 10 Copilot Instructions - These must be strictly followed when generating, editing, or reviewing C# code for this project. Adhere to these guidelines for consistency and maintainability.

## Formatting & Style
- Use consistent indentation (spaces preferred) and always use braces, even for single-line blocks
- Organize using directives alphabetically, grouping System namespaces first
- Do not use default parameters

## Type Usage
- Use `var` only when the type is obvious from the right-hand side; otherwise, use explicit types
- Prefer value types and stackalloc to avoid allocations
- Use Span<T> and Memory<T> for data slices
- Avoid boxing/unboxing and excessive object creation in loops or hot paths
- Do not use string concatenation, prefer string interpolation or StringBuilder for complex cases

## Method Design
- Keep methods small and focused; one method should do one thing only
- Prefer expression-bodied members for simple methods
- Use modern C# features: pattern matching, null-coalescing operators (??, ??=), switch expressions, records
- Prefer ValueTask over Task in performance-critical async methods
- Avoid unnecessary allocations (e.g., avoid ToList() unless required)

## Architecture & Design
- Follow SOLID principles
- Prefer composition over inheritance
- Use Dependency Injection for decoupling and testability
- Apply design patterns (Repository, Strategy, Factory, Adapter) where applicable
- Follow Clean Architecture for large applications

## Documentation
- Always document public methods, properties, and classes with detailed XML comments

## Additional Guidelines
- Structure classes: private members > properties > constructors > methods
- Use primary constructors where possible
- Avoid magic numbers and strings—use constants or enums
- Target .NET 10 and use modern C# features
- Prefer pure C# implementations; only use third-party libraries if necessary and ensure MIT License compatibility
- Ensure code is easy to read, maintain, and extend
- Test thoroughly and write unit tests for critical logic

# Native Methods
- Prefer using CsWin32 (https://github.com/microsoft/CsWin32/) for Windows API interop.
- Always use LibraryImport for P/Invoke when possible, and ensure that all necessary attributes (e.g., SetLastError, CharSet) are correctly applied.
- For any native libraries imported from Github, etc. ensure to add them as submodules to the repository and reference them from the project, rather than relying on external package managers or manual downloads. Ensure they build and are added to Natives directory in the projects folder and copied to the output directory on build.

# Copilot: Apply these instructions when generating, editing, or reviewing C# code for this project. Adhere strictly for consistency and maintainability.