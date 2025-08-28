namespace ActorServer.Messages;

public enum ERROR_MSG_TYPE
{
    ZONE_CHANGE_ERROR = 1,
    PLAYER_MOVE_ERROR = 2,
}


public record ErrorMessage(ERROR_MSG_TYPE Type, string Reason);