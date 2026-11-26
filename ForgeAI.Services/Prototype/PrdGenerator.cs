using ForgeAI.Core.Interfaces;
using ForgeAI.Core.Models;
using Microsoft.Extensions.Logging;

namespace ForgeAI.Services.Prototype;

public class PrdGenerator
{
    private readonly IBedrockService _bedrock;
    private readonly ILogger<PrdGenerator> _logger;

    public PrdGenerator(IBedrockService bedrock, ILogger<PrdGenerator> logger)
    {
        _bedrock = bedrock;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(Opportunity opportunity, Evaluation? evaluation)
    {
        var prompt = $@"You are a product manager writing a mini PRD for an AI coding agent that will build a prototype.

Your goal is to create a focused PRD that produces a convincing prototype in one build session.

OPPORTUNITY:
Title: {opportunity.Title}
Problem: {opportunity.ProblemStatement}
Target audience: {opportunity.TargetAudience}
Why now: {opportunity.WhyNow}
Platform recommendation: {opportunity.WhyMobileFirst}
Evaluation summary: {evaluation?.Summary ?? "N/A"}
Evaluation reasoning: {evaluation?.Reasoning ?? "N/A"}
Overall score: {evaluation?.OverallScore ?? 0}/100

Write a PRD with EXACTLY this structure.

IMPORTANT: Include a ""Why Invest"" section in the PRD. The prototype will display this on a pitch page. Use plain, simple language. Pull from the evaluation reasoning to explain:
- Why this problem is worth solving (market size, frequency, pain level)
- Why existing solutions fail (specific competitor weaknesses if mentioned in reasoning)
- What makes this approach different
- How it could make money

# {{App Title}}

## Target User
One sentence describing the primary user.

## Core Problem
One sentence describing the pain point.

## Primary Value Proposition
One sentence describing what this app gives the user.

## Prototype Goal
1-2 sentences describing what this prototype must demonstrate to validate the concept.
What must be obvious within 10 seconds of opening the app?

## Why Invest
- Why this problem is worth solving (one sentence about market size or pain frequency)
- Why current solutions fail (specific weakness)
- What makes this different (one sentence)
- How it makes money (pricing model)

## Strategic Notes
- Key strengths to emphasize in the prototype
- Key risks to mitigate or avoid showing
- What the prototype should focus on to be convincing

## Platform Direction
- Recommended platform: mobile | web | hybrid
- If mobile: thumb-friendly layout, simple flows, quick actions, stacked cards
- If web: denser layout, dashboard/workspace feel, side nav or wider content areas
- If hybrid: responsive layout, mobile summary + desktop management flow

## Screens (3-5 max)
1. **Screen Name** — purpose of the screen, key UI elements, and primary user action
2. **Screen Name** — purpose of the screen, key UI elements, and primary user action
3. **Screen Name** — purpose of the screen, key UI elements, and primary user action

## Primary User Flow
Step-by-step: what the user does from opening the app to getting value.

## Prototype Type
Specify one: consumer app flow | workflow dashboard | marketplace concept | onboarding + core feature demo | data visualization tool

## UI Style
- Clean, modern design appropriate for the recommended platform
- Specific color palette or style direction
- Emphasize trust, clarity, or speed based on what the opportunity needs
- Typography and spacing that matches the prototype type

## Technical Constraints
- React with Vite
- Mock data only (no backend, no API calls)
- Local state only (useState/useContext)
- No authentication
- Must work in a browser
- Must match the recommended platform style

## Out of Scope
- No extra features beyond the core prototype flow
- No admin panels or settings pages
- No real integrations or third-party services
- No unnecessary complexity or overengineering
- No generic placeholder text — use realistic sample content

Keep it concise but specific. This PRD must be detailed enough for an AI coding agent to build a working, convincing prototype in one session.";

        var prd = await _bedrock.InvokeAsync(prompt);
        _logger.LogInformation("PRD generated for '{Title}' ({Length} chars)", opportunity.Title, prd.Length);
        return prd;
    }
}
