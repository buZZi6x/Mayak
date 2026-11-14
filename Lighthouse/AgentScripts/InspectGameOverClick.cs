using System;
using System.Threading.Tasks;
using LighthouseGame;
using UnityEngine;
using UnityEngine.UIElements;
public static class InspectGameOverClick
{
    public static async Task<object> Main()
    {
        if (!UnityEditor.EditorApplication.isPlaying) { return new { skipped = "Editor is not playing" }; }
        var session = UnityEngine.Object.FindAnyObjectByType<BlockoutSession>();
        var hud = UnityEngine.Object.FindAnyObjectByType<BlockoutHUD>();
        session.BeginRun(); session.EndRun("Топливо кончилось. Маяк погас.");
        await Task.Delay(300);
        int clicks = 0, menus = 0;
        var button = hud.Root.Q<Button>("result-menu");
        button.clicked += () => clicks++;
        session.MenuRequested += () => menus++;
        var position = button.worldBound.center;
        var hit = hud.Root.panel.Pick(position);
        bool before = session.MenuOpen;
        using (var evt = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, mousePosition = position, button = 0, clickCount = 1 }))
        { evt.target = hit; hit.SendEvent(evt); }
        await Task.Delay(60);
        using (var evt = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, mousePosition = position, button = 0, clickCount = 1 }))
        { evt.target = hit; hit.SendEvent(evt); }
        await Task.Delay(2000);
        return new { before, hit = hit.name, clicks, menus, scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, sessionExists = (bool)session, gameOver = session ? session.GameOver : false, menuOpen = session ? session.MenuOpen : false };
    }
}
