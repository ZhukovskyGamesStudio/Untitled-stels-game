using Stealth.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Stealth.UI
{
    /// <summary>Main menu (host / join) and the in-game Esc menu. Placeholder IMGUI.</summary>
    public class GameMenu : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        public static bool SettingsOpen { get; private set; }

        /// <summary>Test hook: opens the pause menu with the settings page.</summary>
        public static void OpenSettingsForTest()
        {
            IsOpen = true;
            SettingsOpen = true;
        }

        string m_Address = "127.0.0.1";

        void Update()
        {
            var net = GameNetwork.Instance;
            if (net == null || Keyboard.current == null) return;
            if (!net.IsRunning) { IsOpen = false; return; }
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (SettingsOpen) CloseSettings();
                else IsOpen = !IsOpen;
            }
        }

        void OnGUI()
        {
            var net = GameNetwork.Instance;
            if (net == null) return;
            GUI.depth = 0;
            UiDraw.Begin();
            if (SettingsOpen && (IsOpen || !net.IsRunning)) DrawSettings();
            else if (!net.IsRunning) DrawMainMenu(net);
            else if (IsOpen) DrawPauseMenu(net);
        }

        void DrawMainMenu(GameNetwork net)
        {
            UiDraw.Rect(new Rect(0, 0, UiDraw.W, UiDraw.H), new Color(0.06f, 0.07f, 0.08f, 1f));
            var area = new Rect(UiDraw.W / 2f - 220, UiDraw.H * 0.16f, 440, 520);
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
            if (GUILayout.Button("Настройки", GUILayout.Height(30))) OpenSettings();
            if (GUILayout.Button("Выход", GUILayout.Height(30))) Application.Quit();
            GUILayout.EndArea();
        }

        void DrawPauseMenu(GameNetwork net)
        {
            UiDraw.Rect(new Rect(0, 0, UiDraw.W, UiDraw.H), new Color(0, 0, 0, 0.6f));
            GUILayout.BeginArea(new Rect(UiDraw.W / 2f - 180, UiDraw.H * 0.3f, 360, 260));
            GUILayout.Label(net.Manager.IsHost ? "Ты хост" : "Ты подключён к хосту");
            if (net.UsingSteam && SteamLobby.Instance != null && SteamLobby.Instance.InLobby &&
                GUILayout.Button("Пригласить друзей (Steam)", GUILayout.Height(36)))
                SteamLobby.Instance.OpenInviteDialog();
            if (GUILayout.Button("Продолжить", GUILayout.Height(36))) IsOpen = false;
            if (GUILayout.Button("Настройки", GUILayout.Height(36))) OpenSettings();
            if (GUILayout.Button("Покинуть игру", GUILayout.Height(36)))
            {
                IsOpen = false;
                net.Leave();
            }
            GUILayout.EndArea();
        }

        static void OpenSettings()
        {
            Stealth.Player.GameSettings.EnsureLoaded();
            SettingsOpen = true;
        }

        static void CloseSettings()
        {
            Stealth.Player.GameSettings.Save();
            SettingsOpen = false;
        }

        void DrawSettings()
        {
            UiDraw.Rect(new Rect(0, 0, UiDraw.W, UiDraw.H), new Color(0.04f, 0.05f, 0.06f, 0.92f));
            var area = new Rect(UiDraw.W / 2f - 230, UiDraw.H * 0.18f, 460, 420);
            UiDraw.Label(new Rect(area.x, area.y - 46, area.width, 40), "Настройки", 26, TextAnchor.MiddleCenter, Color.white, true);
            GUILayout.BeginArea(area);
            GUILayout.Label($"Чувствительность мыши: {Stealth.Player.GameSettings.Sensitivity * 10f:0.0}");
            Stealth.Player.GameSettings.Sensitivity = GUILayout.HorizontalSlider(Stealth.Player.GameSettings.Sensitivity, 0.02f, 0.4f);
            GUILayout.Space(10);
            GUILayout.Label($"Поле зрения: {Stealth.Player.GameSettings.Fov:0}°");
            Stealth.Player.GameSettings.Fov = Mathf.Round(GUILayout.HorizontalSlider(Stealth.Player.GameSettings.Fov, 60f, 100f));
            GUILayout.Space(10);
            Stealth.Player.GameSettings.InvertY = GUILayout.Toggle(Stealth.Player.GameSettings.InvertY, " Инвертировать мышь по вертикали");
            Stealth.Player.GameSettings.HeadBob = GUILayout.Toggle(Stealth.Player.GameSettings.HeadBob, " Покачивание камеры при ходьбе");
            Stealth.Player.GameSettings.CrouchToggle = GUILayout.Toggle(Stealth.Player.GameSettings.CrouchToggle, " Ctrl переключает присед (иначе — удерживать)");
            GUILayout.Space(16);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("По умолчанию", GUILayout.Height(32))) Stealth.Player.GameSettings.ResetToDefaults();
            if (GUILayout.Button("Готово", GUILayout.Height(32))) CloseSettings();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
