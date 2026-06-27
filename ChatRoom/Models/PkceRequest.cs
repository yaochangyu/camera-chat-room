namespace ChatRoom.Models;

public record PkceChallengeRequest(string CodeChallenge);

public record PkceExchangeRequest(string AuthCode, string CodeVerifier);
