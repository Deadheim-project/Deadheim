using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace RaidSystem
{
    class GUI
    {
        private static GameObject _menu;
        private static readonly Dictionary<string, GameObject> _menuItems = new Dictionary<string, GameObject>();
        private static GameObject _scoreboard;

        public static void ToggleMenu()
        {
            if (_menu == null || (_menu != null && !_menu.activeSelf))
            {
                if (GUIManager.Instance == null) { Debug.LogError("[RaidSystem] GUIManager null"); return; }
                if (!GUIManager.CustomGUIFront) { Debug.LogError("[RaidSystem] CustomGUI null"); return; }

                if (string.IsNullOrEmpty(GuildsIntegration.GetOwnGuildName()))
                {
                    Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "Entre em uma guild para usar o RaidSystem.");
                    return;
                }

                LoadMenu();
            }

            if (_menu != null) _menu.SetActive(!_menu.activeSelf);
        }

        public static void ToggleScoreboard()
        {
            if (_scoreboard != null)
                _scoreboard.SetActive(!_scoreboard.activeSelf);
            else
            {
                LoadScoreboard();
                if (_scoreboard != null) _scoreboard.SetActive(true);
            }
        }

        public static void DestroyMenu()
        {
            if (_menu != null) _menu.SetActive(false);
        }

        public static void LoadMenu()
        {
            if (Player.m_localPlayer == null) return;
            if (GUIManager.Instance == null || !GUIManager.CustomGUIFront) return;

            UnityEngine.Object.Destroy(_menu);
            foreach (var item in _menuItems.Values) UnityEngine.Object.Destroy(item);
            _menuItems.Clear();

            _menu = GUIManager.Instance.CreateWoodpanel(
                parent: GUIManager.CustomGUIFront.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(0, 0),
                width: 500, height: 700, draggable: true);
            _menu.SetActive(false);

            var info = RaidSystemPlugin.LocalPlayerInfo;
            string teamName = GuildsIntegration.GetOwnGuildName();
            if (string.IsNullOrEmpty(teamName))
                return;

            string playerName = info?.Nick ?? Player.m_localPlayer.m_nview.GetZDO().GetString("playerName");

            // Scrollview for team members
            GameObject scrollView = GUIManager.Instance.CreateScrollView(
                parent: _menu.transform,
                showHorizontalScrollbar: false,
                showVerticalScrollbar: true,
                handleSize: 8f,
                handleColors: GUIManager.Instance.ValheimScrollbarHandleColorBlock,
                handleDistanceToBorder: 50f,
                slidingAreaBackgroundColor: new Color(0.157f, 0.102f, 0.063f, 1f),
                width: 400f, height: 380f);
            ((RectTransform)scrollView.transform).anchoredPosition = new Vector2(-20, -50);
            scrollView.SetActive(true);
            _menuItems["scrollView"] = scrollView;

            // Guild name
            _menuItems["teamText"] = MakeText(teamName, _menu, new Vector2(200, 650), 30, 350, 40);
            // Player name
            _menuItems["playerNameText"] = MakeText(playerName, _menu, new Vector2(200, 590), 22, 350, 50);

            // Territory count
            var territories = DataStore.Load().Territories;
            int ownCount = territories.Count(t => string.Equals(t.OwnerTeamId, teamName, StringComparison.OrdinalIgnoreCase));
            _menuItems["territoriesText"] = MakeText(
                $"Territories: {ownCount}/{territories.Count}", _menu, new Vector2(370, 617), 18, 350, 40);

            // Description input + Update button
            GameObject descInput = GUIManager.Instance.CreateInputField(
                placeholderText: info?.Description ?? "",
                parent: _menu.transform,
                anchorMin: new Vector2(0, 0), anchorMax: new Vector2(0, 0),
                position: new Vector2(160, 555),
                contentType: InputField.ContentType.Standard,
                fontSize: 18, width: 270f, height: 33f);
            _menuItems["descInput"] = descInput;

            GameObject btnUpdate = GUIManager.Instance.CreateButton(
                text: "Update", parent: _menu.transform,
                anchorMin: new Vector2(0, 0), anchorMax: new Vector2(0, 0),
                position: new Vector2(360, 555), width: 90, height: 35f);
            btnUpdate.SetActive(true);
            _menuItems["btnUpdate"] = btnUpdate;

            InputField inputComp = descInput.GetComponent<InputField>();
            inputComp.text = info?.Description ?? "";
            btnUpdate.GetComponent<Button>().onClick.AddListener(() =>
                RPCManager.SendPlayerRegistration(inputComp.text));

            // Guild members header
            _menuItems["teamMembersHeader"] = MakeText("Guild Members:", _menu, new Vector2(200, 496), 25, 350, 40);

            // Populate scrollview
            FillTeamMembers(scrollView, teamName);
            var sr = scrollView.transform.Find("Scroll View")?.GetComponent<ScrollRect>();
            if (sr != null) sr.verticalNormalizedPosition = 1f;

            // Close button
            GameObject btnClose = GUIManager.Instance.CreateButton(
                text: "Close", parent: _menu.transform,
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(0, -300f), width: 170, height: 45f);
            btnClose.SetActive(true);
            _menuItems["btnClose"] = btnClose;
            btnClose.GetComponent<Button>().onClick.AddListener(DestroyMenu);
        }

        public static void UpdateScoreboard()
        {
            if (_scoreboard != null && _scoreboard.activeSelf)
                LoadScoreboard();
        }

        private static void LoadScoreboard()
        {
            if (GUIManager.Instance == null || !GUIManager.CustomGUIFront) return;

            bool wasActive = _scoreboard != null && _scoreboard.activeSelf;
            UnityEngine.Object.Destroy(_scoreboard);

            _scoreboard = GUIManager.Instance.CreateWoodpanel(
                parent: GUIManager.CustomGUIFront.transform,
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(400, 0), width: 500, height: 600, draggable: true);

            MakeText(RaidSystemPlugin.ScoreboardTitle.Value, _scoreboard, new Vector2(250, 560), 28, 450, 40);

            var top = ScoreManager.GetTopPlayers();
            int y = 510;
            for (int i = 0; i < top.Count && y > 100; i++, y -= 28)
            {
                var s = top[i];
                MakeText($"#{i + 1} {s.Nick} [{s.TeamId}]  K:{s.Kills} D:{s.Deaths} = {s.TotalPoints}pts",
                    _scoreboard, new Vector2(250, y), 15, 460, 25);
            }

            y -= 10;
            MakeText("--- Guild Ranking ---", _scoreboard, new Vector2(250, y), 18, 460, 25);
            y -= 35;
            foreach (var (teamId, pts, members) in ScoreManager.GetTeamRanking())
            {
                if (y < 50) break;
                MakeText($"{teamId} ({members} members): {pts} pts", _scoreboard, new Vector2(250, y), 15, 460, 25);
                y -= 25;
            }

            GameObject btnClose = GUIManager.Instance.CreateButton(
                text: "Close", parent: _scoreboard.transform,
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(0, -270f), width: 150, height: 40f);
            btnClose.SetActive(true);
            btnClose.GetComponent<Button>().onClick.AddListener(() => _scoreboard.SetActive(false));

            _scoreboard.SetActive(wasActive);
        }

        private static void FillTeamMembers(GameObject scrollView, string teamName)
        {
            Transform content = scrollView.transform.Find("Scroll View/Viewport/Content");
            if (content == null) return;

            var members = GuildsIntegration.GetTeamMemberNicks(teamName);
            foreach (string nick in members)
            {
                GUIManager.Instance.CreateText(
                    text: nick,
                    parent: content,
                    anchorMin: new Vector2(0.5f, 1f), anchorMax: new Vector2(0.5f, 1f),
                    position: new Vector2(0f, 0f),
                    font: GUIManager.Instance.AveriaSerifBold,
                    fontSize: 20, color: GUIManager.Instance.ValheimOrange,
                    outline: true, outlineColor: Color.black,
                    width: 300f, height: 25f, addContentSizeFitter: false);
            }
        }

        private static GameObject MakeText(string text, GameObject parent, Vector2 pos, int size, float w, float h)
            => GUIManager.Instance.CreateText(
                text: text, parent: parent.transform,
                anchorMin: new Vector2(0, 0), anchorMax: new Vector2(0, 0),
                position: pos, font: GUIManager.Instance.AveriaSerifBold,
                fontSize: size, color: GUIManager.Instance.ValheimOrange,
                outline: true, outlineColor: Color.black,
                width: w, height: h, addContentSizeFitter: false);
    }
}
