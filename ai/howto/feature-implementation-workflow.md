# Feature Implementation Workflow

This document describes the standard workflow for implementing new features in the PRo3D.Viewer project. Follow this guide to ensure consistent, well-documented, and high-quality feature implementations.

## Core Implementation Workflow

**CRITICAL**: For every feature implementation, you MUST follow this exact workflow:

0. **Always follow the PRIME DIRECTIVES from CLAUDE.md**

1. **Create a Detailed Plan Document FIRST**:
   - Create a markdown file in `ai/plans/` with date prefix: `YYYY-MM-DD_feature-name.md`
   - Example: `2024-12-18-csv-export.md`, `2024-12-18-verbose-logging.md`
   - Include detailed requirements, design decisions, implementation steps, and success criteria
   - Use existing documents in `ai/plans/` folder as examples
   - This plan will be continuously updated and will become the final comprehensive report

2. **Maintain Strict Quality Standards**:
   - **0 ERRORS, 0 WARNINGS POLICY** - This MUST NOT BE VIOLATED
   - Build and test after each significant change
   - Fix any issues immediately before proceeding

3. **Document Progress Continuously**:
   - Update the plan document after EACH implementation step
   - Record what was done, why, and any learnings
   - Include code snippets, file paths, and line numbers
   - Document any adaptations to the plan based on new findings

4. **Complete the Implementation**:
   - NEVER STOP WORKING until everything is finished
   - Do not interrupt to give status reports - continue until done
   - Adapt the plan as needed based on discoveries during implementation

5. **Finalize Documentation**:
   - Update README.md with the new feature (extremely terse, no-nonsense style)
   - Review the plan document one final time to ensure it's a complete record
   - The plan should have transformed into a comprehensive implementation report

## Detailed Implementation Process

### 1. Analyze Requirements
- Clarify the feature's purpose and expected behavior
- Identify key functional and non-functional requirements
- Determine success criteria and acceptance conditions
- Ask for clarification if requirements are ambiguous or incomplete

### 2. Design the Implementation
- Study the existing codebase structure and patterns
- Follow project-specific guidelines from CLAUDE.md if available
- Identify the appropriate modules and files to modify
- Design the feature to integrate seamlessly with existing architecture
- Consider edge cases, error handling, and validation needs
- Plan for backward compatibility when relevant

### 3. Write the Code
- Implement the feature incrementally, starting with core functionality
- Follow the project's coding standards and conventions
- Use existing patterns and utilities where applicable
- Add appropriate error handling and input validation
- Ensure code is readable, maintainable, and well-structured
- Include inline comments for complex logic

### 4. Integration Considerations
- Ensure the feature works with existing functionality
- Update configuration files if needed
- Modify CLI interfaces, APIs, or user interfaces as required
- Consider performance implications and optimize where necessary
- Update any affected documentation strings or help text

### 5. Quality Assurance
- **ENFORCE 0 ERRORS, 0 WARNINGS POLICY** - Build frequently and fix issues immediately
- Self-review the implementation for correctness and completeness
- Verify the feature meets all stated requirements
- Check for potential bugs or logic errors
- Ensure consistent naming conventions and code style
- Validate that error messages are clear and helpful
- Document all testing and validation in the plan document

### 6. Implementation Approach
- Present a clear implementation plan before making changes
- Explain key design decisions and trade-offs
- Show the specific code changes with context
- Highlight any dependencies or prerequisites
- Suggest testing approaches for the new feature

## Critical Work Principles

Always follow these principles:
- **ULTRATHINK** about requirements and implementation approach before starting
- **WORK CONTINUOUSLY** - Never stop until the feature is completely implemented
- **DOCUMENT AS YOU GO** - Update the plan document after each step, not at the end
- **MAINTAIN 0 ERRORS, 0 WARNINGS** - Build frequently and fix immediately
- **NEVER INTERRUPT** to give status reports - complete the work first
- Prefer modifying existing files over creating new ones unless absolutely necessary
- Maintain consistency with the existing codebase style and patterns
- Focus on implementing exactly what was requested without adding unnecessary features
- Provide clear explanations in the plan document of what changes were made and why
- Alert the user to any potential issues or considerations in the plan document

## Technology-Specific Guidelines

### F# Projects
- Use functional programming patterns, discriminated unions, and immutable data structures
- Follow existing namespace and module organization
- Use pattern matching effectively
- Maintain immutability where possible

### CLI Applications
- Follow established argument parsing patterns (Argu library)
- Update help text and usage documentation
- Ensure proper error messages for invalid arguments
- Test command combinations thoroughly

### Web Applications
- Ensure proper request handling, validation, and response formatting
- Follow RESTful conventions where applicable
- Include proper error handling and status codes

### Data Processing
- Consider performance, memory usage, and scalability
- Use streaming/lazy evaluation for large datasets
- Implement proper cleanup and resource disposal

## Plan Document Structure

The plan document in `ai/plans/` (named as `YYYY-MM-DD_feature-name.md`) should follow this structure:

1. **Overview**: Brief description of the feature
2. **Requirements**: Detailed functional and non-functional requirements
3. **Design Decisions**: Key architectural and implementation choices
4. **Implementation Plan**: Step-by-step breakdown of tasks
5. **Implementation Progress**: Continuously updated section documenting:
   - Each phase with status (PENDING → IN PROGRESS → COMPLETED)
   - Actual code changes with file paths and line numbers
   - Build/test results after each step
   - Any issues encountered and how they were resolved
   - Adaptations to the original plan
6. **Testing**: Validation procedures and results
7. **Lessons Learned**: Key takeaways from the implementation
8. **Final Summary**: What was achieved, statistics, final status

The plan document should be updated IN REAL TIME as work progresses. Each completed step should be immediately documented with concrete details. See existing files in `ai/plans/` for examples of the expected quality and detail level.

## Final Steps

At the END of implementation:
1. **Update README.md** - Add the new feature in an extremely terse, no-nonsense style (look at existing README.md for the expected style)
2. **Final Plan Review** - Check the plan document one final time to ensure it's fully updated and reflects the complete implementation journey
3. **Verify Success** - Ensure all requirements are met, build succeeds with 0 errors/0 warnings, and the feature works as expected

## Goal

The goal is to deliver a working, well-integrated feature that enhances the application while maintaining code quality and consistency, with comprehensive documentation that serves as a complete historical record of the implementation.

## Usage

To use this workflow, reference this document when starting a new feature:
```
"Read docs/howto/feature-implementation-workflow.md and then help me add [feature]"
"Following the workflow in docs/howto/feature-implementation-workflow.md, let's implement [feature]"
```