using Microsoft.EntityFrameworkCore;
using Remedy.Shared.Data;
using Remedy.Shared.Models;

namespace Remedy.Cli.Services;

public class ResourceMatchingService(RemedyDbContext context)
{
    /// <summary>
    /// Calculates priority decay based on multiple factors
    /// </summary>
    public double CalculatePriorityDecay(Resource resource, DateTime currentTime)
    {
        var daysOld = (currentTime - resource.SavedAt).TotalDays;

        // Base decay curve (exponential)
        var baseDecay = Math.Exp(-0.1 * daysOld);

        // Snooze penalty (geometric)
        var snoozePenalty = Math.Pow(0.8, resource.TimesSnoozed);

        // Recency boost if recently reminded
        var recencyBoost = 1.0;
        if (resource.LastReminded.HasValue)
        {
            var daysSinceReminder = (currentTime - resource.LastReminded.Value).TotalDays;
            recencyBoost = daysSinceReminder < 2 ? 1.5 : 1.0;
        }

        return resource.Priority * baseDecay * snoozePenalty * recencyBoost;
    }

    /// <summary>
    /// Calculates energy level match score (0.0 to 1.0)
    /// </summary>
    private double MatchEnergy(Resource resource, UserContext userContext)
    {
        var resourceEnergy = (int)resource.MinEnergyLevel;
        var currentEnergy = (int)userContext.CurrentEnergy;

        // If current energy meets or exceeds requirement, full score
        if (currentEnergy >= resourceEnergy)
            return 1.0;

        // Otherwise, reduce score based on deficit
        var deficit = resourceEnergy - currentEnergy;
        return Math.Max(0.0, 1.0 - (deficit * 0.3));
    }

    /// <summary>
    /// Calculates time slot match score (0.0 to 1.0)
    /// </summary>
    private double MatchTimeSlot(Resource resource, UserContext userContext)
    {
        // If no active time slot, use partial score
        if (!userContext.ActiveTimeSlotId.HasValue)
            return 0.5;

        // If resource has no preferred time slot, use partial score
        if (!resource.PreferredTimeSlotId.HasValue)
            return 0.5;

        // Perfect match
        if (resource.PreferredTimeSlotId == userContext.ActiveTimeSlotId)
            return 1.0;

        // No match
        return 0.3;
    }

    /// <summary>
    /// Simple context relevance based on string similarity
    /// For MVP, uses basic string contains check
    /// </summary>
    private double CalculateContextRelevance(Resource resource, UserContext userContext)
    {
        if (string.IsNullOrWhiteSpace(userContext.CurrentContextDescription))
            return 0.5;

        if (string.IsNullOrWhiteSpace(resource.CreatedByContext))
            return 0.5;

        var contextLower = userContext.CurrentContextDescription.ToLowerInvariant();
        var resourceContextLower = resource.CreatedByContext.ToLowerInvariant();

        // Check for common words
        var contextWords = contextLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var resourceWords = resourceContextLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var commonWords = contextWords.Intersect(resourceWords).Count();
        if (commonWords == 0)
            return 0.3;

        var maxWords = Math.Max(contextWords.Length, resourceWords.Length);
        return 0.5 + (0.5 * commonWords / maxWords);
    }

    /// <summary>
    /// Calculates composite score for a resource based on user context
    /// </summary>
    public double CalculateCompositeScore(Resource resource, UserContext userContext)
    {
        var daysOld = (userContext.CurrentTime - resource.SavedAt).TotalDays;

        var basePriority = CalculatePriorityDecay(resource, userContext.CurrentTime);
        var timeDecay = Math.Max(0.1, 1.0 - (daysOld * 0.05)); // Gradual decay
        var energyMatch = MatchEnergy(resource, userContext);
        var slotMatch = MatchTimeSlot(resource, userContext);
        var snoozePenalty = resource.TimesSnoozed * -0.1;
        var contextRelevance = CalculateContextRelevance(resource, userContext);

        // Weighted composite score
        var score = (basePriority * 0.3) +
                    (timeDecay * 0.2) +
                    (energyMatch * 0.2) +
                    (slotMatch * 0.15) +
                    (contextRelevance * 0.15) +
                    snoozePenalty;

        return Math.Max(0.0, score);
    }

    /// <summary>
    /// Gets optimal resources matching the current user context
    /// </summary>
    public async Task<List<Resource>> GetOptimalResourcesAsync(UserContext userContext, int count = 3)
    {
        // Filter candidates by basic criteria
        var candidates = await context.Resources
            .Include(r => r.PreferredTimeSlot)
            .Where(r => !r.IsCompleted)
            .Where(r => r.EstimatedTimeMinutes <= userContext.AvailableDurationMinutes)
            .Where(r => (int)r.MinEnergyLevel <= (int)userContext.CurrentEnergy)
            .ToListAsync();

        // Calculate scores for all candidates
        foreach (var resource in candidates)
        {
            resource.ComputedScore = CalculateCompositeScore(resource, userContext);
        }

        // Return top N by score
        return candidates
            .OrderByDescending(r => r.ComputedScore)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Updates resource priority decay for all resources
    /// </summary>
    public async Task UpdateAllPrioritiesAsync()
    {
        var resources = await context.Resources
            .Where(r => !r.IsCompleted)
            .ToListAsync();

        var currentTime = DateTime.Now;

        foreach (var resource in resources)
        {
            var decayedPriority = CalculatePriorityDecay(resource, currentTime);
            resource.Priority = decayedPriority;
        }

        await context.SaveChangesAsync();
    }
}
