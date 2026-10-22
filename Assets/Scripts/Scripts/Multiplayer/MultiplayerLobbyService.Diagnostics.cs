using System;
using System.Collections.Generic;
using System.IO;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class MultiplayerLobbyService
{
    [Serializable] public sealed class ConnectionSample
    {
        public float elapsed, peakFrameMilliseconds, timeScale;
        public int ping, jitter, reliableResends, incomingQueue, outgoingQueue;
        public bool focused, paused;
        public string reachability, state;
    }
    [Serializable] public sealed class ConnectionReport
    {
        public string utc, cause, scene, transport, lastRetryCause, recovery = "Pending";
        public int attempts;
        public List<ConnectionSample> samples;
    }
    public DisconnectCause LastDisconnectCause { get; private set; }
    public string LastDisconnectReportPath { get; private set; }
    readonly List<ConnectionSample> connectionSamples = new List<ConnectionSample>();
    ConnectionReport lastConnectionReport;
    float nextConnectionSample, peakFrameMilliseconds;
    bool applicationPaused, applicationFocused = true;
    void ObserveTransportState(ClientState previous, ClientState next) { SampleConnectionDiagnostics(true); }
    void OnApplicationPause(bool paused) { applicationPaused = paused; SampleConnectionDiagnostics(true); }
    void OnApplicationFocus(bool focused) { applicationFocused = focused; SampleConnectionDiagnostics(true); }
    void SampleConnectionDiagnostics(bool force = false)
    {
        if (!onlineSession && !recovering && pending == Request.None) return;
        peakFrameMilliseconds = Mathf.Max(peakFrameMilliseconds, Time.unscaledDeltaTime * 1000);
        if (!force && Time.unscaledTime < nextConnectionSample) return;
        nextConnectionSample = Time.unscaledTime + 1;
        var peer = PhotonNetwork.NetworkingClient.LoadBalancingPeer;
        connectionSamples.Add(new ConnectionSample { elapsed = Time.realtimeSinceStartup, ping = peer.RoundTripTime,
            jitter = peer.RoundTripTimeVariance, reliableResends = peer.ResentReliableCommands,
            incomingQueue = peer.QueuedIncomingCommands, outgoingQueue = peer.QueuedOutgoingCommands,
            focused = applicationFocused, paused = applicationPaused, peakFrameMilliseconds = peakFrameMilliseconds, timeScale = Time.timeScale,
            reachability = Application.internetReachability.ToString(), state = PhotonNetwork.NetworkClientState.ToString() });
        peakFrameMilliseconds = 0;
        if (connectionSamples.Count > 32) connectionSamples.RemoveAt(0);
    }
    void RecordConnectionLoss(DisconnectCause cause)
    {
        if (!onlineSession || leaving || cause == DisconnectCause.DisconnectByClientLogic || cause == DisconnectCause.ApplicationQuit) return;
        SampleConnectionDiagnostics(true);
        if (recovering && lastConnectionReport != null)
        {
            lastConnectionReport.lastRetryCause = cause.ToString(); lastConnectionReport.attempts = recoveryAttempt;
            lastConnectionReport.samples = new List<ConnectionSample>(connectionSamples); WriteConnectionReport(); return;
        }
        LastDisconnectCause = cause;
        lastConnectionReport = new ConnectionReport { utc = DateTime.UtcNow.ToString("o"), cause = cause.ToString(),
            scene = SceneManager.GetActiveScene().path, transport = PhotonNetwork.NetworkingClient.LoadBalancingPeer.TransportProtocol.ToString(),
            samples = new List<ConnectionSample>(connectionSamples) };
        WriteConnectionReport();
        Debug.LogWarning("[Multiplayer] Connection interrupted: " + cause + ". Local diagnostics: " + LastDisconnectReportPath);
    }
    void RecordRecoveryResult(bool success)
    {
        if (lastConnectionReport == null) return;
        lastConnectionReport.recovery = success ? "Recovered" : "Failed"; lastConnectionReport.attempts = recoveryAttempt;
        WriteConnectionReport();
    }
    void WriteConnectionReport()
    {
        try
        {
            string folder = Path.Combine(Application.persistentDataPath, "NetworkDiagnostics"); Directory.CreateDirectory(folder);
            LastDisconnectReportPath = Path.Combine(folder, "latest-disconnect.json");
            // Bounded local report; no account IDs, invite codes, tokens, chat or application credentials.
            File.WriteAllText(LastDisconnectReportPath, JsonUtility.ToJson(lastConnectionReport, true));
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        { Debug.LogWarning("[Multiplayer] Could not save local diagnostics: " + error.GetType().Name); }
    }
    static string DisconnectExplanation(DisconnectCause cause)
    {
        string explanation = cause == DisconnectCause.ClientTimeout || cause == DisconnectCause.ServerTimeout
            ? "The connection timed out." : "The multiplayer connection was interrupted.";
        return explanation + " Check your connection, then host or join again. Reason: " + cause + ".";
    }
}
