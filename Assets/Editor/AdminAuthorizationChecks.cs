using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

// Isolated Edit Mode fixtures. No scene loads, sign-ins or account writes.
public static class AdminAuthorizationChecks
{
    public static async void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run outside Play Mode.");
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var authFields = typeof(AdminAuthorization).GetFields(flags).Where(f => !f.IsLiteral && !f.IsInitOnly).ToArray();
        var saved = authFields.Select(f => f.GetValue(null)).ToArray();
        var backend = typeof(TrialBackendClient);
        var transport = backend.GetField("ValidationTransport", flags);
        var uid = backend.GetField("ValidationUid", flags);
        var email = backend.GetField("ValidationEmail", flags);
        object oldTransport = transport.GetValue(null), oldUid = uid.GetValue(null), oldEmail = email.GetValue(null);
        var lines = new System.Collections.Generic.List<string>();
        Action<bool, string> check = (ok, message) => { if (!ok) throw new Exception(message); lines.Add("PASS " + message); };
        try
        {
            uid.SetValue(null, "admin-fixture");
            foreach (var address in new[] { "sngbdsa137@gmail.com", "russelbarrameda593@gmail.com", "famatigabarrameda@gmail.com", "gravata.joshuaharvey@ncst.edu.ph", "cayangyang.jhayfel@ncst.edu.ph" })
            {
                email.SetValue(null, " " + address.ToUpperInvariant() + " ");
                transport.SetValue(null, (Func<string, string, Task<string>>)((p, j) => Task.FromResult("{\"admin\":true,\"uid\":\"admin-fixture\"}")));
                AdminAuthorization.Refresh(); AdminAuthorization.Tick();
                check(AdminAuthorization.IsAuthorized, "normalized admin " + address);
            }
            transport.SetValue(null, (Func<string, string, Task<string>>)((p, j) => Task.FromResult("{\"admin\":false,\"uid\":\"admin-fixture\"}")));
            AdminAuthorization.Refresh(); AdminAuthorization.Tick();
            check(!AdminAuthorization.IsAuthorized, "backend denial overrides allowlisted email");
            transport.SetValue(null, (Func<string, string, Task<string>>)((p, j) => Task.FromException<string>(new Exception("fixture offline"))));
            AdminAuthorization.Refresh(); AdminAuthorization.Tick();
            check(!AdminAuthorization.IsAuthorized && AdminAuthorization.ConnectionStatus.Contains("Retrying"), "connection failure stays denied and explains recovery");
            transport.SetValue(null, (Func<string, string, Task<string>>)((p, j) => Task.FromResult("{\"admin\":true,\"uid\":\"admin-fixture\"}")));
            typeof(AdminAuthorization).GetField("refreshAt", flags).SetValue(null, 0f);
            AdminAuthorization.Tick();
            check(AdminAuthorization.IsAuthorized && AdminAuthorization.ConnectionStatus.Contains("confirmed"), "retry recovers after connection failure");
            var pending = new TaskCompletionSource<string>();
            transport.SetValue(null, (Func<string, string, Task<string>>)((p, j) => pending.Task));
            AdminAuthorization.Refresh(); AdminAuthorization.Tick();
            check(!AdminAuthorization.IsAuthorized, "pending access never grants permissions");
            uid.SetValue(null, "ordinary-fixture"); email.SetValue(null, "ordinary@example.invalid");
            AdminAuthorization.Tick();
            pending.SetResult("{\"admin\":true,\"uid\":\"admin-fixture\"}");
            await Task.Yield();
            check(!AdminAuthorization.IsAuthorized, "late response cannot grant access after account switch");
            lines.Add("ALL ADMIN AUTHORIZATION CHECKS PASSED");
        }
        catch (Exception error) { lines.Add("FAIL " + error.Message); Debug.LogException(error); }
        finally
        {
            transport.SetValue(null, oldTransport); uid.SetValue(null, oldUid); email.SetValue(null, oldEmail);
            for (int i = 0; i < authFields.Length; i++) authFields[i].SetValue(null, saved[i]);
            System.IO.File.WriteAllLines("Library/AdminAuthorizationChecks.txt", lines);
        }
    }
}
