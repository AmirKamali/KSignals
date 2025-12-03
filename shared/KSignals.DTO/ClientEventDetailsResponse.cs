using System.Collections.Generic;

namespace KSignals.DTO;

/// <summary>
/// Response for getting event details with nested markets
/// </summary>
public class ClientEventDetailsResponse
{
    /// <summary>
    /// Gets or Sets Event
    /// </summary>
    public ClientEventData Event { get; set; } = new ClientEventData();

    /// <summary>
    /// Data for the markets in this event.
    /// </summary>
    public List<ClientEvent> Markets { get; set; } = new List<ClientEvent>();
}
