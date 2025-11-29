namespace web_asp.Models;

public record FirebaseUserInfo(
    string FirebaseId,
    string? Email,
    string? DisplayName,
    string? FirstName,
    string? LastName);
