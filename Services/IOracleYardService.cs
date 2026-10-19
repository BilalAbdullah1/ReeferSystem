using ReeferSystem.Models;

namespace ReeferSystem.Services
{
    public interface IOracleYardService
    {
        Task<(string Status, List<ContainerModel> Containers)> GetAllContainersAsync(string userId = "ADMIN", bool forceRefresh = false);
        Task<(string Status, CvDetailsModel? Details)> GetCvDetailsAsync(string ctrNbr, string userId = "ADMIN");
        Task<(string Status, long Useq, string? ErrorMessage)> InsertYardSlotAsync(YardSlotModel slot);
        Task<(string Status, string? ErrorMessage)> UpdateYardSlotAsync(YardSlotModel slot);
        Task<(string Status, string? ErrorMessage)> DeleteYardSlotAsync(long useq, string userId = "1");
        Task<List<YardSlotModel>> GetYardSlotsAsync();
        Task<(string Status, string? ErrorMessage)> UpdateContainerPositionAsync(string ctrNbr, string newPosition);
    }
}
