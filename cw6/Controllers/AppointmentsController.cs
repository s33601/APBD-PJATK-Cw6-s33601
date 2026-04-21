using System.Data;
using cw6.DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.JSInterop.Infrastructure;

namespace cw6.Controllers;
 [ApiController]
    [Route("api/[controller]")]
public class AppointmentsController : ControllerBase
{
    private IConfiguration configuration;
       public  AppointmentsController(IConfiguration config)
        {
            configuration = config;
        }
       
       
       //wyjecie wszystkich
        [HttpGet]
        public async Task<IActionResult> GetAppointments( string? status,  string? patientLastName,int? idDoctor)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            await using var connection = new SqlConnection(connectionString);
           await connection.OpenAsync();
           var sql1 = """
                      
                     SELECT 
                         a.IdAppointment, 
                         a.AppointmentDate, 
                         a.Status, 
                         a.Reason, 
                         p.FirstName + N' ' + p.LastName AS PatientFullName, 
                         p.Email AS PatientEmail 
                     FROM dbo.Appointments a 
                     JOIN dbo.Patients p ON p.IdPatient = a.IdPatient 
                     WHERE (@Status IS NULL OR a.Status = @Status) 
                       AND (@PatientLastName IS NULL OR p.LastName = @PatientLastName) 
                     AND (@IdDoctor IS NULL OR a.IdDoctor = @IdDoctor)
                     ORDER BY a.AppointmentDate;
                     """;
            await using var command = new SqlCommand(sql1,connection);
               var statusToAdd = command.Parameters.Add("@Status", SqlDbType.NVarChar);
                if (status != null)
                {
                    statusToAdd.Value = status;
                }
                else
                {
                    statusToAdd.Value = DBNull.Value;
                }
                var lastNameToAdd = command.Parameters.Add("@PatientLastName", SqlDbType.NVarChar);
                if (patientLastName != null)
                {
                    lastNameToAdd.Value = patientLastName;
                }
                else
                {
                    lastNameToAdd.Value = DBNull.Value;
                }
                var idDoctorToAdd = command.Parameters.Add("@IdDoctor", SqlDbType.Int);
                if (idDoctor != null)
                {
                    idDoctorToAdd.Value = idDoctor;
                }
                else
                {
                    idDoctorToAdd.Value = DBNull.Value;
                }

                var listaAppointments = new List<AppointmentListDto>();
                await using var wartosci = await command.ExecuteReaderAsync();

                while (await wartosci.ReadAsync())
                {
                    var AppListDTO = new AppointmentListDto();
                    AppListDTO.IdAppointment = wartosci.GetInt32(wartosci.GetOrdinal("IdAppointment"));
                    AppListDTO.AppointmentDate = wartosci.GetDateTime(wartosci.GetOrdinal("AppointmentDate"));
                    AppListDTO.Status = wartosci.GetString(wartosci.GetOrdinal("Status"));
                    AppListDTO.Reason = wartosci.GetString(wartosci.GetOrdinal("Reason"));
                    AppListDTO.PatientFullName = wartosci.GetString(wartosci.GetOrdinal("PatientFullName"));
                    AppListDTO.PatientEmail = wartosci.GetString(wartosci.GetOrdinal("PatientEmail"));                    
                  
                    listaAppointments.Add(AppListDTO);
                }
                    return Ok(listaAppointments);
        }
      
        
        //wyjecie po id
        [HttpGet("{id}")]
        public async Task<IActionResult> GetAppointmentsById( int? id)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            await using var connection = new SqlConnection(connectionString);
           await connection.OpenAsync();
           var sql1 = """
                      
                     SELECT 
                         a.IdAppointment, 
                             a.IdPatient,
                                 a.IdDoctor,
                         a.AppointmentDate, 
                         d.LicenseNumber,
                         p.Email,
                         p.PhoneNumber,
                         a.Status, 
                         a.Reason, 
                         a.InternalNotes,
                        a.CreatedAt
                     FROM dbo.Appointments a 
                     JOIN dbo.Patients p ON p.IdPatient = a.IdPatient 
                     JOIN dbo.Doctors d on d.IdDoctor = a.IdDoctor
                     WHERE  a.IdAppointment = @IdAppointment
                     ORDER BY a.AppointmentDate;
                     """;
            await using var command = new SqlCommand(sql1,connection);
            
            command.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = id;
              
                await using var wartosci = await command.ExecuteReaderAsync();

                var czyMamy = await wartosci.ReadAsync();
                if (czyMamy == false)
                {
                    return NotFound(new ErrorResponseDto { Message = "Nie ma takiej wizyty" });
                }
                var AppDetilsDot = new AppointmentDetailsDto();
                
                AppDetilsDot.IdAppointment = wartosci.GetInt32(wartosci.GetOrdinal("IdAppointment"));
                AppDetilsDot.IdPatient = wartosci.GetInt32(wartosci.GetOrdinal("IdPatient"));
                AppDetilsDot.Email = wartosci.GetString(wartosci.GetOrdinal("Email"));
                AppDetilsDot.PhoneNumber = wartosci.GetString(wartosci.GetOrdinal("PhoneNumber"));
                AppDetilsDot.LicenseNumber = wartosci.GetString(wartosci.GetOrdinal("LicenseNumber"));
                AppDetilsDot.IdDoctor = wartosci.GetInt32(wartosci.GetOrdinal("IdDoctor"));
                AppDetilsDot.AppointmentDate = wartosci.GetDateTime(wartosci.GetOrdinal("AppointmentDate"));
                AppDetilsDot.Status = wartosci.GetString(wartosci.GetOrdinal("Status"));
                AppDetilsDot.Reason = wartosci.GetString(wartosci.GetOrdinal("Reason"));
              int notatka = wartosci.GetOrdinal("InternalNotes");
              if(!await wartosci.IsDBNullAsync(notatka))
                AppDetilsDot.InternalNotes = wartosci.GetString(notatka);
              
                AppDetilsDot.CreatedAt = wartosci.GetDateTime(wartosci.GetOrdinal("CreatedAt"));                    

            return Ok(AppDetilsDot);
        }
        
        
        //dodanie nowego
        [HttpPost]
        public async Task<IActionResult> PostAppointment([FromBody]CreateAppointmentRequestDto dto)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            await using var connection = new SqlConnection(connectionString);
           await connection.OpenAsync();

           if (dto.AppointmentDate < DateTime.Now)
           {
               return BadRequest(new ErrorResponseDto { Message = "Nieprawidlowa data" });
           }

           if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Length > 250)
           {
               return BadRequest(new ErrorResponseDto { Message = "Opis jest wymagany i moze miec maksymalnie 250 znaków" });
           }

           var sql1 = """
                      SELECT COUNT(1) 
                      FROM dbo.Patients 
                      WHERE IdPatient = @IdPatient AND IsActive = 1; 
                      """;
            await using var CzyPacjetnAktywny = new SqlCommand(sql1,connection);
            CzyPacjetnAktywny.Parameters.Add("@IdPatient", SqlDbType.Int).Value = dto.IdPatient;

            var patientCount = (int)await CzyPacjetnAktywny.ExecuteScalarAsync();

            if (patientCount == 0)
            {
                return NotFound(new ErrorResponseDto { Message = "Nie ma takiego pacjenta"});
            }
            
            var sqlLekarz = """
                            SELECT COUNT(1)
                            FROM dbo.Doctors
                            WHERE IdDoctor = @IdDoctor AND IsActive = 1;
                            """;
            await using var sprawdzLekarza = new SqlCommand(sqlLekarz, connection);
            sprawdzLekarza.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = dto.IdDoctor;
            var ileLekarzy = (int)await sprawdzLekarza.ExecuteScalarAsync();

            if (ileLekarzy == 0)
            {
                return NotFound(new ErrorResponseDto{Message = "Brak aktywnego lekarza o podanym ID" });
            }
            
           var sql2 = """
                                                SELECT COUNT(*)
                                                FROM dbo.Appointments a 
                                                WHERE (@IdDoctor = a.IdDoctor) AND (a.AppointmentDate = @AppointmentDate)
                                                """;
          await using var CzyWolnyTermin = new SqlCommand(sql2,connection);
          CzyWolnyTermin.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = dto.IdDoctor;
          CzyWolnyTermin.Parameters.Add("@AppointmentDate", SqlDbType.DateTime).Value = dto.AppointmentDate;
           
         var dateCount = (int)await CzyWolnyTermin.ExecuteScalarAsync();

         if (dateCount >= 1)
         {
             return Conflict(new ErrorResponseDto{Message = "Termin nie jest dostepny"});
         }
         
         var sql3 = """
                    INSERT INTO dbo.Appointments(idPatient, idDoctor, AppointmentDate, Reason, InternalNotes, Status) VALUES
                    (@IdPatient, @IdDoctor, @AppointmentDate, @Reason, @InternalNotes, 'Scheduled')

                     
                    """;
         
         await using var Dodawanieta = new SqlCommand(sql3,connection);
         Dodawanieta.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = dto.IdDoctor;
         Dodawanieta.Parameters.Add("@IdPatient", SqlDbType.Int).Value = dto.IdPatient;
         Dodawanieta.Parameters.Add("@AppointmentDate", SqlDbType.DateTime).Value = dto.AppointmentDate;
         Dodawanieta.Parameters.Add("@Reason", SqlDbType.NVarChar).Value = dto.Reason;
         
         Dodawanieta.Parameters.Add("@InternalNotes", SqlDbType.NVarChar).Value = dto.InternalNotes != null? dto.InternalNotes : (object)DBNull.Value;

         await Dodawanieta.ExecuteNonQueryAsync();
         return Created();

        }
        //aktuwalizowanie po id
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAppointment(int id, [FromBody] UpdateAppointmentRequestDto dto)
        {
            if (dto.Status != "Scheduled" && dto.Status != "Completed" && dto.Status != "Cancelled")
                      {
                          return BadRequest(new ErrorResponseDto{Message = "Niepoprawny status wizyty"});
                      } 
            if (dto.AppointmentDate < DateTime.Now)
            {
                return BadRequest(new ErrorResponseDto { Message = "Nieprawidlowa data" });
            }

            if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Length > 250)
            {
                return BadRequest(new ErrorResponseDto { Message = "Opis jest wymagany i moze miec maksymalnie 250 znaków" });
            }
             var connectionString = configuration.GetConnectionString("DefaultConnection");
            await using var connection = new SqlConnection(connectionString);
           await connection.OpenAsync();
           var sql1 = """
                      SELECT Status, AppointmentDate
                      FROM dbo.Appointments a
                      WHERE a.IdAppointment = @Id;
                      """;
            await using var czyWizytaIstnieje = new SqlCommand(sql1,connection);
            czyWizytaIstnieje.Parameters.Add("@Id", SqlDbType.Int).Value = id;
            await using var czytanie = await czyWizytaIstnieje.ExecuteReaderAsync();

            if (!await czytanie.ReadAsync())
            {
                return NotFound(new ErrorResponseDto{Message = "Nie ma takiej wizyty"});
            }
            

            var obecnyStatus = czytanie.GetString(czytanie.GetOrdinal("Status"));
            var obecnaData = czytanie.GetDateTime(czytanie.GetOrdinal("AppointmentDate"));
            
            await czytanie.CloseAsync();

            if (obecnyStatus == "Completed" && obecnaData != dto.AppointmentDate)
            {
                return Conflict(new ErrorResponseDto{Message = "Nie mozna zmienic terminu zakonczonej wizyty"});
            }
            

            var sql2 = """
                        
                       SELECT COUNT(1)
                       FROM dbo.Patients p
                       WHERE  p.IdPatient = @IdPatient AND p.IsActive = 1;
                       """;
            
            await using var CzyIstneiejPacjent = new SqlCommand(sql2,connection);
            CzyIstneiejPacjent.Parameters.Add("@IdPatient", SqlDbType.Int).Value = dto.IdPatient;
            var pacjent =  (int)await CzyIstneiejPacjent.ExecuteScalarAsync();
            if (pacjent == 0)
            {
                return NotFound(new ErrorResponseDto{Message = "Nie ma takiego pacjenta"});
            }

            var sql3 = """
                        
                       SELECT COUNT(1)
                       FROM dbo.Doctors d
                       WHERE  d.IdDoctor = @IdDoctor AND d.IsActive = 1;
                       """;
            
            await using var czyDoktorIstneije = new SqlCommand(sql3,connection);
            czyDoktorIstneije.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = dto.IdDoctor;
            var doctor = (int)await czyDoktorIstneije.ExecuteScalarAsync();
            if (doctor == 0)
            {
                return NotFound(new ErrorResponseDto{Message = "Nie ma takiego doktora"});
            }

            var sql4 = """
                       SELECT  COUNT(1)
                       FROM dbo.Appointments a
                       WHERE a.IdDoctor = @IdDoctor AND 
                             a.AppointmentDate = @AppointmentDate AND
                             a.IdAppointment != @Id
                       """;
            await using var czyZmianaDaty = new SqlCommand(sql4,connection);
            czyZmianaDaty.Parameters.Add("@AppointmentDate", SqlDbType.DateTime).Value = dto.AppointmentDate;
            czyZmianaDaty.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = dto.IdDoctor;
            czyZmianaDaty.Parameters.Add("@Id", SqlDbType.Int).Value = id;
            var zmiana = (int)await czyZmianaDaty.ExecuteScalarAsync();
            if (zmiana >= 1)
            {
                return Conflict(new ErrorResponseDto{Message = "Podany termin jest juz zajety"});
            }


            var update = """
                            UPDATE dbo.Appointments SET 
                                IdPatient = @IdPatient,
                                IdDoctor = @IdDoctor,
                                AppointmentDate = @AppointmentDate,
                                Status = @Status,
                                Reason = @Reason,
                                InternalNotes = @InternalNotes
                            WHERE IdAppointment = @Id
                            """;
            await using var updateRobimy = new SqlCommand(update, connection);
            updateRobimy.Parameters.Add("@Id", SqlDbType.Int).Value = id;
            updateRobimy.Parameters.Add("@IdPatient", SqlDbType.Int).Value = dto.IdPatient;
            updateRobimy.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = dto.IdDoctor;
            updateRobimy.Parameters.Add("@AppointmentDate", SqlDbType.DateTime).Value = dto.AppointmentDate;
            updateRobimy.Parameters.Add("@Status", SqlDbType.NVarChar).Value = dto.Status;
            updateRobimy.Parameters.Add("@Reason", SqlDbType.NVarChar).Value = dto.Reason;

            updateRobimy.Parameters.Add("@InternalNotes", SqlDbType.NVarChar).Value =
                dto.InternalNotes != null ? dto.InternalNotes : (object)DBNull.Value;
            
            await updateRobimy.ExecuteNonQueryAsync();
            return Ok(dto);


        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAppointment(int id)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            var sqlCzyIstnieje = """
                                 SELECT Status
                                 FROM dbo.Appointments
                                 WHERE IdAppointment = @IdAppointment
                                 """;
            await using var command =  new SqlCommand(sqlCzyIstnieje,connection);
            command.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = id;
            await using var czytanie = await command.ExecuteReaderAsync();
            if(!await czytanie.ReadAsync()) 
                return NotFound(new ErrorResponseDto{Message = "Nie ma takiej wizyty"});

            var obecnyStatus = czytanie.GetString(czytanie.GetOrdinal("Status"));
            await czytanie.CloseAsync();

            if (obecnyStatus == "Completed")
            {
                return Conflict(new ErrorResponseDto{Message = "Nie mozna usunac zakonczonej wizyty"});
            }

            var usuwanie = """
                      DELETE
                      FROM dbo.Appointments 
                             WHERE  IdAppointment = @IdAppointment
                      """;
            await using var wizyta =  new SqlCommand(usuwanie, connection);
           
            wizyta.Parameters.Add("@IdAppointment",SqlDbType.Int).Value = id;
           await wizyta.ExecuteNonQueryAsync();

           return NoContent();


        }
}