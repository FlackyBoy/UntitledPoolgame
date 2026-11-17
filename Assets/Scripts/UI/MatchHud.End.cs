using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Core
{
    // The winner card and "Revanche".
    public partial class MatchHud
    {
        // ---------- End of the match ----------

        private void UpdateEnd(PoolMatchRules rules, float dt)
        {
            // Boogie Night's wheel takes the screen once the card is pressed.
            bool over = rules != null && rules.MatchStarted && rules.GameOver && !rules.RestartPending && !BoogieWheel.IsOpen;
            endLayer.style.display = over ? DisplayStyle.Flex : DisplayStyle.None;
            if (!over)
            {
                endShownAt = -1f;
                return;
            }

            float now = Time.unscaledTime;
            if (endShownAt < 0f)
            {
                endShownAt = now;
                int w = Mathf.Max(0, rules.Winner);
                string subtitle = rules.Setup.Describe();
                string title = Format(cfg.winnerFormat, w + 1);
                string button = cfg.rematchText;
                if (BoogieNight.Active)
                {
                    if (boogieRecorded != rules.MatchGeneration)
                    {
                        boogieRecorded = rules.MatchGeneration;
                        BoogieNight.Record(rules.Winner);
                    }
                    BoogieNightSettings boogie = BoogieNightSettings.Instance;
                    subtitle += "\n" + Format(boogie.scoreFormat, BoogieNight.Wins(0), BoogieNight.Wins(1));
                    button = boogie.nextButton;
                    // The night is won: the card is the night's winner's.
                    if (BoogieNight.Decided)
                    {
                        w = BoogieNight.Leader;
                        title = Format(boogie.nightWinnerFormat, w + 1);
                        button = boogie.menuButton;
                    }
                }
                endWhoBox.style.backgroundColor = cfg.PlayerColor(w);
                TextFx.Set(endWho, Format(cfg.playerTagFormat, w + 1));
                TextFx.Reveal(endTitle, title);
                TextFx.Set(endSubtitle, subtitle);
                TextFx.Set(endButtonText, button);
                foreach (Half h in halves) h.plateKey = default;
            }
            float t = now - endShownAt;

            // Card pops in, rays turn, confetti falls once.
            float k = Mathf.Clamp01(t / 0.6f);
            float s = Mathf.Lerp(0.3f, 1f, BackOut(k));
            endCard.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            endCard.style.scale = new Scale(new Vector3(s, s, 1f));
            endCard.style.rotate = new Rotate(new Angle(-6f * (1f - k), AngleUnit.Degree));
            endCard.style.opacity = k;
            endRays.style.rotate = new Rotate(new Angle(t * 12f, AngleUnit.Degree));
            for (int i = 0; i < confetti.Count; i++)
            {
                float ct = Mathf.Clamp01((t - (i % 10) * 0.12f) / 3.2f);
                confetti[i].style.top = Length.Percent(-4f + 110f * ct);
                confetti[i].style.rotate = new Rotate(new Angle(720f * ct + i * 37f, AngleUnit.Degree));
                confetti[i].style.opacity = ct >= 1f ? 0f : 1f;
            }
            float pulse = 1f + 0.04f * Mathf.Sin(t * 5f);
            endButton.style.scale = new Scale(new Vector3(pulse, pulse, 1f));

            if (t > 0.6f && RevanchePressed())
            {
                if (!BoogieNight.Active) rules.RequestRestart();
                else if (!BoogieNight.Decided) BoogieWheel.Open();
                else GameSession.LoadMainMenu();
            }
        }

        private bool RevanchePressed()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame))
                return true;
            // A pad nobody plays with is someone joining (PlayerDeviceRouter),
            // not a rematch.
            foreach (Gamepad pad in Gamepad.all)
                if ((pad.buttonSouth.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame)
                    && UnityEngine.InputSystem.Users.InputUser.FindUserPairedToDevice(pad) != null) return true;
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && root.panel != null)
            {
                Vector2 screen = mouse.position.ReadValue();
                Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
                if (endButton.worldBound.Contains(panelPos)) return true;
            }
            return false;
        }
    }
}
