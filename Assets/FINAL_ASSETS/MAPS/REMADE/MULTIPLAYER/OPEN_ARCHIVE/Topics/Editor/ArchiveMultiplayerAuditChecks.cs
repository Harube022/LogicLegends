using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveMultiplayerAuditChecks
    {
        public static object ValidatePauseCleanup()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Use Play Mode with the real gameplay UI.");
            var menu=UnityEngine.Object.FindFirstObjectByType<LevelMenu>();
            var input=GameInput.Instance;var player=Player.LocalInstance;
            if(menu==null||input==null||player==null)throw new InvalidOperationException("Gameplay controls are not ready.");
            var panel=(GameObject)typeof(LevelMenu).GetField("pausePanel",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(menu);
            if(panel==null||panel.activeSelf)throw new InvalidOperationException("Begin with Settings closed.");
            bool blocked=input.GameplayInputBlocked,playerEnabled=player.enabled,menuEnabled=menu.enabled;
            float scale=Time.timeScale;var checks=new List<string>();
            try
            {
                menu.OpenPause();
                if(!panel.activeSelf||!input.GameplayInputBlocked||player.enabled)throw new InvalidOperationException("Settings did not own the local input block.");
                checks.Add("Settings blocks the local player while open.");
                menu.enabled=false;
                if(panel.activeSelf||input.GameplayInputBlocked!=blocked||player.enabled!=playerEnabled||Time.timeScale!=scale)
                    throw new InvalidOperationException("Disabling Settings leaked its input, control or time state.");
                checks.Add("External Settings shutdown restores the previous input, player and time state.");
                menu.enabled=menuEnabled;menu.OpenPause();menu.ClosePause();
                if(input.GameplayInputBlocked!=blocked||player.enabled!=playerEnabled||Time.timeScale!=scale)
                    throw new InvalidOperationException("Settings Resume no longer restores controls.");
                checks.Add("Normal Resume remains functional after re-enabling the menu.");
                var result=new{passed=true,checks};
                Directory.CreateDirectory("Temp/MultiplayerAudit");
                File.WriteAllText("Temp/MultiplayerAudit/PauseCleanup.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
                return result;
            }
            finally
            {
                menu.ClosePause();menu.enabled=menuEnabled;
                input.SetGameplayInputBlocked(blocked);player.ToggleControl(playerEnabled);Time.timeScale=scale;
            }
        }
    }
}
