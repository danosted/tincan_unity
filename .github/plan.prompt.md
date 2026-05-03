---
name: plan
description: "Generate and refine a plan.md file for a new feature or change without implementing it."
argument-hint: "Describe the feature or change you want to plan..."
agent: agent
---
# Task
Your objective is to generate and refine a `plan.md` file for the requested feature or change.

# Rules
1. **DO NOT start implementation.** Your sole purpose right now is to scope the architecture, steps, and requirements.
2. Create or update a `plan.md` (or similarly named Markdown file) in the appropriate `.docs/plans/` directory for this project.
3. Outline the steps clearly, noting any dependencies, relevant files to touch, and verification criteria.
4. Ask the user for feedback to refine the plan. Wait for the user to explicitly say "approved" or "ok to implement" before proceeding with any code changes.
