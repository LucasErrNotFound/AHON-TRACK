using AHON_TRACK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AHON_TRACK.Services.Interface
{
    public interface ITrainingService
    {
        Task<List<TrainingModel>> GetTrainingSchedulesAsync();
        Task<bool> AddTrainingScheduleAsync(TrainingModel training);
        Task<bool> UpdateTrainingScheduleAsync(TrainingModel training);
        Task<bool> UpdateAttendanceAsync(int trainingID, string attendance);
        Task<bool> DeleteTrainingScheduleAsync(int trainingID);
        Task<TrainingModel?> GetTrainingScheduleByIdAsync(int trainingID);
        Task<List<TraineeModel>> GetAvailableTraineesAsync();

        // Load for coach assignment
        Task<List<(int CoachID, string FullName, string Username)>> GetCoachNamesAsync();
        Task<List<string>> GetPackageNamesAsync();
    }
}
