namespace cw6.DTO;

public class AppointmentDetailsDto
{
    public int IdAppointment { get; set; }
    public int IdPatient { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public string LicenseNumber { get; set; }
    public int IdDoctor { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string InternalNotes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}