using System;
using System.Collections.Generic;
using System.IO;
using LogicLegends.Tests.Khezu;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveLegacyTestGuardChecks
    {
        [Serializable] public sealed class Report
        {
            public bool passed;
            public string scope="Native permission-policy checks. Release and guest cases exercise the pure build/session policy, not an Android release executable or second rendered guest.";
            public List<string> checks=new List<string>(),errors=new List<string>();
        }
        public static Report Validate()
        {
            var report=new Report();
            void Check(bool pass,string name)
            {report.checks.Add((pass?"PASS ":"FAIL ")+name);if(!pass)report.errors.Add(name);}
            Check(!KhezuLegacyTestPermission.Allows(false,false,false),
                "An isolated release build cannot use legacy test controls");
            Check(!KhezuLegacyTestPermission.Allows(false,true,true),
                "An authoritative release host cannot use legacy test controls");
            Check(!KhezuLegacyTestPermission.Allows(true,true,false),
                "A development guest in the Archive cannot use legacy test controls");
            Check(KhezuLegacyTestPermission.Allows(true,true,true),
                "An authoritative development Archive host retains test controls");
            Check(KhezuLegacyTestPermission.Allows(true,false,false),
                "An isolated development test scene retains its existing controls");
            var session=ArchiveTopicSession.Current;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            const bool compiledDevelopment=true;
#else
            const bool compiledDevelopment=false;
#endif
            Check(KhezuLegacyTestPermission.CanUse==(Application.isPlaying&&
                KhezuLegacyTestPermission.Allows(compiledDevelopment,session!=null,session!=null&&session.CanUseTopicAdmin)),
                "Runtime permission uses the actual compiled build and existing authoritative admin permission");
            report.passed=report.errors.Count==0;
            Directory.CreateDirectory("Temp/MultiplayerAudit");
            File.WriteAllText("Temp/MultiplayerAudit/LegacyTestGuard.json",JsonUtility.ToJson(report,true));
            return report;
        }
    }
}
