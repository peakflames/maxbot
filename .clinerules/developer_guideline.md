# Mogzi Project Developer Guidelines

## References

- **Architecture** - `docs/process/03_architecture.md`
- **Design** - `docs/process/04_00_design.md`

## Coding Standard and Conventions

1. All non-test code is enforeced by `src/.editorconfig`. The must MUST be read by developer before making code changes.
2. All compile warnings MUST be addressed.
3. ULTRA IMPORTANT: prefer Global Usings over File Usings. prefer the Result pattern via FluentResult package. prefer designs that avoid null pointers.

## Debugging

- runtime logs are located at `logs located at ~/.mogzi/logs/`
- **HYPER ULTRA IMPORTANT:** `dotnet run` results in a interactive terminal session, therefore when running the application prefer to ask the user to run there tool manually and provide feedback via text, screenshot, or you can inspect the logs

## Testing Infrastruction (Mogzi.TUI)

__Test Infrastructure Classes:__

- `TestApplicationBuilder.cs` - Sets up real FlexColumnTuiApp with test configuration
- `KeyboardSimulator.cs` - Simulates real keyboard events (ConsoleKey, character input)
- `RenderingCapture.cs` - Captures and verifies UI output from components
- `TestConfigurationProvider.cs` - Provides test-friendly configuration
- `TestLoggerProvider.cs` - Custom logging for tests

__Test Approach:__

- No-mocking approach using actual FlexColumnTuiApp instances
- Real service configuration and DI container
- Actual component rendering (captured for verification)
- Black-box testing focused on user workflows and system behavior

__Root Cause Protocol:__

- After completing root cause analysis of test failures, prefer to prsent the results making it clear if it is a application or test issue to the user and use the `ask_followup_question` tool for the user's confirmation

## Context Window Usage Rule

- If the 'Context Window Usage' provided by the user's environment is >= 50% utilization, then `ask_followup_question` to ask th euser whether to use the `new_task` tool to continue i a new task 