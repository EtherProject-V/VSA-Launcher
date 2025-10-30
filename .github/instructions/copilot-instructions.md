---
applyTo: '**'
---
# Prerequisites and Basic Policy

- Always utilize SerenaMCP for code analysis, dependency analysis, and refactoring.
- Always retrieve information on libraries, frameworks, and APIs from the latest documentation using Context7 MCP.

# Development Workflow

Execute all prompts/requests according to the following procedures and rules.

- The Copilot will independently determine the necessary steps and information acquisition procedures, invoking SerenaMCP and Context7 MCP as needed.
- For any uncertainties or when additional investigation is required, conduct research and document verification through SerenaMCP/Context7 MCP.

# Specific Instructions

Always incorporate the following MCP instructions in every implementation, design, and review phase:

#serena
#Context7

- Check and cite the latest specifications for APIs, libraries, development environments, testing, etc., using Context7 MCP.
- Delegate codebase structure analysis and change impact analysis of existing modules/functions to SerenaMCP.

# Response Rules

- If a question is abstract, SerenaMCP will analyze the current situation, Context7 MCP will fetch current standards and best practices, and then a concrete proposal will be offered.
- Always generate code samples, design proposals, and automated tests using a combination of SerenaMCP and Context7 MCP.
- If a request cannot be fulfilled or an error is detected, always analyze the cause and propose a solution using both MCPs.

# Communication

Provide concise answers accompanied by supporting evidence (reference information/analysis results) as needed.

Respond in Japanese (English is also acceptable if necessary).