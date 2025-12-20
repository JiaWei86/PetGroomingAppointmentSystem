using System;

namespace PetGroomingAppointmentSystem.Areas.Admin.Services;

/// <summary>
/// Phone number formatting and validation service interface for Admin area
/// </summary>
public interface IPhoneService
{
    /// <summary>
    /// Format phone number to standard Malaysian format: 01X-XXXXXXX or 01X-XXXXXXXX
    /// </summary>
    string FormatPhoneNumber(string phoneNumber);

    /// <summary>
    /// Validate if phone number is in correct Malaysian format
    /// </summary>
    bool ValidatePhoneFormat(string phoneNumber);

    /// <summary>
    /// Clean phone number (remove all non-digits)
    /// </summary>
    string CleanPhoneNumber(string phoneNumber);
}
