namespace ChatRoom.Models;

public record CallOfferDto(string TargetUsername, string SdpOffer, string CallType);
public record CallAnswerDto(string CallerUsername, string SdpAnswer);
public record IceCandidateDto(string TargetUsername, string Candidate);
