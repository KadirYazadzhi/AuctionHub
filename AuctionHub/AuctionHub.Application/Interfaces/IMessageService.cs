using AuctionHub.Application.DTOs;

namespace AuctionHub.Application.Interfaces;

public interface IMessageService
{
    Task<IEnumerable<ContactMessageDto>> GetAllAsync();
    Task CreateAsync(ContactMessageDto model);
    Task MarkReadAsync(int id);
    Task DeleteAsync(int id);
}
