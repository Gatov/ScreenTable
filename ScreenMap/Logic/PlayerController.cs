using ScreenMap.Logic.Messages;

namespace ScreenMap.Logic;

public class PlayerController
{
    PlayersMap _playersMap;
    public void ProcessMessage(IMessage message)
    {
        switch (message)
        {
            case RevealAtMessage revealAt: _playersMap.RevealAt(revealAt.Location, revealAt.BrushSize, revealAt.Reveal);
                break; 
        }
    }
}