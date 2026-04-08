using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Models;

namespace NotificationService.Services;

public class NotificationPreferenceService
{
    private readonly NotificationDbContext _db;

    public NotificationPreferenceService(NotificationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<NotificationPreference>> GetPreferences(Guid userId)
    {
        return await _db.NotificationPreferences
            .Where(p => p.UserId == userId)
            .ToListAsync();
    }

    public async Task UpdatePreference(Guid userId, string eventType, bool emailEnabled, bool smsEnabled)
    {
        var existing = await _db.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.EventType == eventType);

        if (existing is null)
        {
            _db.NotificationPreferences.Add(new NotificationPreference
            {
                UserId = userId,
                EventType = eventType,
                EmailEnabled = emailEnabled,
                SmsEnabled = smsEnabled
            });
        }
        else
        {
            existing.EmailEnabled = emailEnabled;
            existing.SmsEnabled = smsEnabled;
        }

        await _db.SaveChangesAsync();
    }
}
