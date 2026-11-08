using Stealth.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Stealth.UI
{
    /// <summary>Main menu (host / join) and the in-game Esc menu. Placeholder IMGUI.</summary>
    public class GameMenu : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        string m_Address = "127.0.0.1";

        void Update()
        {
            var net = GameNetwork.Instance;
            if (net == null || Keyboard.current == null) return;
            if (!net.IsRunning) { IsOpen = false; return; }
            if (Keyboard.current.escapeKey.wasPressedThisFrame) IsOpen = !IsOpen;
        }

        void OnGUI()
        {
            var net = GameNetwork.Instance;
            if (net == null) return;
            GUI.depth = 0;
            if (!net.IsRunning) DrawMainMenu(net);
            else if (IsOpen) DrawPauseMenu(net);
        }

        void DrawMainMenu(GameNetwork net)
        {
            UiDraw.Rect(new Rect(0, 0, Screen.width, Screen.height), new Color(0.06f, 0.07f, 0.08f, 1f));
            var area = new Rect(Screen.width / 2f - 220, Screen.height * 0.16f, 440, 520);
            UiDraw.Label(new Rect(area.x, area.y, area.width, 50), "АЙТИШНИКИ: ВЫЛАЗКА", 30, TextAnchor.MiddleCenter, Color.white, true);
            UiDraw.Label(new Rect(area.x, area.y + 46, area.width, 24), "прототип кор-лупа", 14, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.6f));

            GUILayout.BeginArea(new Rect(area.x, area.y + 96, area.width, 420));
            GUILayout.Label($"Steam: {(SteamManager.Initialized ? "подключён как " + SteamManager.PersonaName : "недоступен — " + SteamManager.Error)}");
            GUI.enabled = SteamManager.Initialized;
            if (GUILayout.Button("Создать игру через Steam", GUILayout.Height(44))) SteamLobby.Instance.HostLobby();
            GUILayout.Label("Друзья заходят через приглашение или «Присоединиться к игре» в Steam.");
            GUI.enabled = true;

            GUILayout.Space(18);
            GUILayout.Label("Локальная сеть / тест на одном ПК (UDP, порт " + net.LocalPort + "):");
            if (GUILayout.Button("Создать локальную игру", GUILayout.Height(36))) net.StartHostLocal();
            GUILayout.BeginHorizontal();
            m_Address = GUILayout.TextField(m_Address, GUILayout.Height(32), GUILayout.Width(220));
            if (GUILayout.Button("Подключиться по IP", GUILayout.Height(32))) net.StartClientLocal(m_Address);
            GUILayout.EndHorizontal();

            GUILayout.Space(18);
            if (!string.IsNullOrEmpty(net.Status)) GUILayout.Label(net.Status);
            if (GUILayout.Button("Выход", GUILayout.Height(30))) Application.Quit();
            GUILayout.EndArea();
        }

        void DrawPauseMenu(GameNetwork net)
        {
            UiDraw.Rect(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.6f));
            GUILayout.BeginArea(new Rect(Screen.width / 2f - 180, Screen.height * 0.3f, 360, 260));
            GUILayout.Label(net.Manager.IsHost ? "Ты хост" : "Ты подключён к хосту");
            if (net.UsingSteam && SteamLobby.Instance != null && SteamLobby.Instance.InLobby &&
                GUILayout.Button("Пригласить друзей (Steam)", GUILayout.Height(36)))
                SteamLobby.Instance.OpenInviteDialog();
            if (GUILayout.Button("Продолжить", GUILayout.Height(36))) IsOpen = false;
            if (GUILayout.Button("Покинуть игру", GUILayout.Height(36)))
            {
                IsOpen = false;
                net.Leave();
            }
            GUILayout.EndArea();
        }
    }
}
