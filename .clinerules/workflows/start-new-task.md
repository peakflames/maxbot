<task name="Start New Task">

<task_objective>
Prepare an AI model to work effectively on the [REDACTED] project by ensuring comprehensive understanding of the project architecture, design patterns, and implementation details, then gather the user's task objective. The expected output is a fully prepared AI model ready to work productively within established [REDACTED] project patterns.
</task_objective>

<detailed_sequence_steps>

# Start New Task Process - Detailed Sequence of Steps

## 1. Project Context Preparation

1. Use the `read_file` tool to read `docs/process/01_concept_of_operation.md` to understand the project's purpose and operational concepts.

2. Use the `read_file` tool to read `docs/process/02_operational_requirements.md` to understand the functional and technical requirements.

3. Use the `read_file` tool to read `docs/process/03_architecture.md` to understand the system architecture, patterns, and component relationships.

4. Use the `read_file` tool to read `docs/process/04_00_design.md` to understand the detailed implementation design and coding patterns.

5. Use the `read_file` tool to read `docs/process/05_ai_tool_op_requirements.md` to understand the AI tool operational requirements.

## 2. Codebase Familiarization

**IMPORTANT NOTE**: The `src/Cli/` project is obsolete and should not be examined. Focus only on the current [REDACTED] implementation.

1. Use the `read_file` tool to examine the solution structure by reading `src/[REDACTED]`.

2. Use the `read_file` tool to review the main project dependencies in `src/[REDACTED]`.

3. Use the `read_file` tool to understand the global imports in `src/[REDACTED]`.

4. Use the `list_files` tool to analyze the core domain models in `src/[REDACTED]`.

5. Use the `list_files` tool to review the service layer in `src/[REDACTED]`.

6. Use the `list_files` tool to examine the tool implementations in `src/[REDACTED]`.

## 5. Key Information Retention

1. Retain understanding of architecture patterns:
    - Dependency Injection with Microsoft.Extensions.DI throughout
    - Factory Pattern for ChatClient creation with Result<T> error handling
    - Strategy Pattern for fuzzy matching in diff operations
    - Functional Error Handling with FluentResults pattern
    - Streaming Architecture using IAsyncEnumerable for real-time updates

2. Retain understanding of technology stack:
    - .NET 9 with C# and AOT compilation
    - Spectre.Console for terminal UI
    - Microsoft.Extensions.AI for AI integration
    - FluentResults for error handling
    - OpenAI SDK for AI services
    - SharpToken for token counting

3. Retain understanding of security model:
    - Working directory enforcement for all file operations
    - Tool approval system (readonly/all modes)
    - Path validation and permission checking
    - Structured XML tool responses

4. Retain understanding of project structure:
    - [REDACTED] dir: Core library with domain, services, and tools
    - [REDACTED] dir: Terminal user interface application
    - [REDACTED] dir: Terminal interface abstraction
    - [REDACTED] dir: Unit and integration tests

5. Retain understanding of development practices:
    - Test-driven development with comprehensive test coverage
    - Requirements traceability with verification methods
    - Security-first approach with approval mechanisms
    - Cross-platform compatibility (Windows, macOS, Linux)

## 6. Task Objective Gathering

1. Use the `ask_followup_question` tool to ask the user: "I have reviewed the project documentation and codebase. I understand the architecture, design patterns, and development practices. What is your objective for today's task?"

2. Wait for the user's response to understand their specific task objective.

3. Confirm understanding of the task objective and any specific requirements or constraints.

## 7. Carry out the requested task

1. Carry out servicing the user's task per your system instructions.

2. if their request is to gather more information then do not consider this the user ultimate objective the task and will likely be revealed in follow up requests.

3. Use the `ask_followup_question` tool to ask the user: "Should I perform this request in a simulate PLAN MODE?"

4. Continue according to their response.

## 8. Update Architecture and Design Documentation

1. Use the `ask_followup_question` tool to ask the user: "Should I update the arch and design documents to reflect the change made during this task?"

2. Continue according to their response.

REMINDER: Prefer to only update to reflect the implemenation and AVOID documenting anything else

## 8. Task Completion

1. When using the `attempt_completion` tool to present the final result, include a suggested git commit message in the result.

2. The git commit message should be concise, descriptive, and follow conventional commit format (e.g., "feat:", "fix:", "refactor:", etc.).

3. Format the commit message as a code block within the result for easy copying.

</detailed_sequence_steps>

</task>
