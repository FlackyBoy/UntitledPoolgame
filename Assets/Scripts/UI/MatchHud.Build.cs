using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UntitledPoolGame.Pool;

namespace UntitledPoolGame.Core
{
    // Building the elements: each player's half and the end card.
    public partial class MatchHud
    {
        // ---------- Building ----------

        private void Build()
        {
            root = document.rootVisualElement;
            Fill(root);
            root.pickingMode = PickingMode.Ignore;

            halvesLayer = Box(root, "halves");
            Fill(halvesLayer);
            halvesLayer.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < halves.Length; i++) halves[i] = BuildHalf(i);
            BuildFree();

            BuildEnd();
        }

        private Half BuildHalf(int index)
        {
            var h = new Half();
            h.frame = Box(halvesLayer, "half" + index);
            h.frame.style.position = Position.Absolute;
            h.frame.style.overflow = Overflow.Hidden;
            h.frame.style.display = DisplayStyle.None;

            h.content = Box(h.frame, "content");
            h.content.style.position = Position.Absolute;
            h.content.style.left = h.content.style.top = 0;
            h.content.style.transformOrigin = new TransformOrigin(0, 0);

            // The player waiting for their turn: their half is dimmed.
            h.dim = Box(h.content, "dim");
            Fill(h.dim);
            h.dim.style.backgroundColor = cfg.waitingDimColor;

            // Plate: player colour + what they're playing for.
            VisualElement plate = PopCard(h.content, "plate");
            plate.style.position = Position.Absolute;
            plate.style.left = Length.Percent(3);
            plate.style.top = Length.Percent(4);
            plate.style.flexDirection = FlexDirection.Row;
            plate.style.alignItems = Align.Center;
            plate.style.paddingLeft = plate.style.paddingTop = plate.style.paddingBottom = 6;
            plate.style.paddingRight = 14;
            h.who = Box(plate, "who");
            h.who.style.width = h.who.style.height = 54;
            h.who.style.justifyContent = Justify.Center;
            h.who.style.marginRight = 10;
            Radius(h.who, 11);
            Border(h.who, 3, Ink);
            h.whoText = Text(h.who, "J1", titleFont, 24, Color.white);
            h.plateBalls = Box(plate, "balls");
            h.plateBalls.style.flexDirection = FlexDirection.Row;
            h.plateBalls.style.alignItems = Align.Center;

            // "À toi !" tag.
            h.tag = Box(h.content, "turn-tag");
            h.tag.style.position = Position.Absolute;
            h.tag.style.left = Length.Percent(4);
            h.tag.style.top = Length.Percent(17);
            h.tag.style.paddingLeft = h.tag.style.paddingRight = 16;
            h.tag.style.paddingTop = h.tag.style.paddingBottom = 4;
            Radius(h.tag, 9);
            h.tagText = Text(h.tag, cfg.yourTurnText, titleFont, cfg.turnTagFontSize, Ink);

            // Power in stock: a ring with the type's colour and icon.
            VisualElement slot = h.slot = Box(h.content, "power-slot");
            slot.style.position = Position.Absolute;
            slot.style.right = Length.Percent(3);
            slot.style.top = Length.Percent(4);
            slot.style.alignItems = Align.Center;
            // The whole block grows from its top-right corner (it stays
            // anchored there): rings, icons, name, key, second power.
            slot.style.transformOrigin = new TransformOrigin(Length.Percent(100), Length.Percent(0), 0f);
            slot.style.scale = new Scale(Vector3.one * Mathf.Max(0.1f, cfg.powerSlotScale));
            h.ring = Box(slot, "ring");
            h.ring.style.width = h.ring.style.height = 84;
            h.ring.style.justifyContent = Justify.Center;
            h.ring.style.alignItems = Align.Center;
            Radius(h.ring, 42);
            h.ringIcon = Box(h.ring, "icon");
            h.ringIcon.style.width = h.ringIcon.style.height = 46;
            h.ringIcon.generateVisualContent += ctx => { if (h.iconTexture == null) DrawPowerIcon(ctx, h.iconType); };
            h.powerName = Text(slot, "—", bodyFont, 20, Color.white);
            h.powerName.style.marginTop = 4;
            InkShadow(h.powerName, 3);
            h.powerKey = Text(slot, cfg.powerKeyText, bodyFont, 16, Ink);
            h.powerKey.style.backgroundColor = Color.white;
            h.powerKey.style.paddingLeft = h.powerKey.style.paddingRight = 8;
            h.powerKey.style.marginTop = 3;
            Radius(h.powerKey, 6);
            Border(h.powerKey, 3, Ink);
            h.switchKey = Text(slot, cfg.powerSwitchKeyText, bodyFont, 14, Color.white);
            h.switchKey.style.marginTop = 2;
            InkShadow(h.switchKey, 2);

            // The other power in stock: a smaller ring to the left of the
            // selected one.
            h.secondRing = Box(slot, "second-ring");
            h.secondRing.style.position = Position.Absolute;
            h.secondRing.style.left = -58;
            h.secondRing.style.top = 22;
            h.secondRing.style.width = h.secondRing.style.height = 48;
            h.secondRing.style.justifyContent = Justify.Center;
            h.secondRing.style.alignItems = Align.Center;
            Radius(h.secondRing, 24);
            h.secondRing.style.opacity = 0.85f;
            h.secondIcon = Box(h.secondRing, "icon");
            h.secondIcon.style.width = h.secondIcon.style.height = 26;
            h.secondIcon.generateVisualContent += ctx => { if (h.secondIconTexture == null) DrawPowerIcon(ctx, h.secondIconType); };
            h.levelBadge = LevelBadge(h.ring, 22);
            h.secondLevelBadge = LevelBadge(h.secondRing, 15);

            // Power gauge with the strike point (while aiming).
            h.power = Box(h.content, "power");
            h.power.style.position = Position.Absolute;
            h.power.style.left = Length.Percent(21);
            h.power.style.width = Length.Percent(58);
            h.power.style.bottom = Length.Percent(5);
            h.power.style.flexDirection = FlexDirection.Row;
            h.power.style.alignItems = Align.Center;
            h.spin = Box(h.power, "spin");
            h.spin.style.width = h.spin.style.height = 60;
            h.spin.style.flexShrink = 0;
            h.spin.style.marginRight = 12;
            h.spin.style.backgroundColor = Hex("#e6e6e6");
            Radius(h.spin, 30);
            Border(h.spin, 3, Ink);
            h.spinDot = Box(h.spin, "dot");
            h.spinDot.style.position = Position.Absolute;
            h.spinDot.style.width = h.spinDot.style.height = 14;
            h.spinDot.style.backgroundColor = Red;
            h.spinDot.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            Radius(h.spinDot, 7);
            VisualElement bar = Box(h.power, "bar");
            bar.style.flexGrow = 1;
            bar.style.height = 38;
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.backgroundColor = Cream;
            bar.style.paddingLeft = bar.style.paddingRight = bar.style.paddingTop = bar.style.paddingBottom = 4;
            Radius(bar, 20);
            Border(bar, 4, Ink);
            bar.style.borderBottomWidth = 8;
            for (int i = 0; i < Segments; i++)
            {
                VisualElement seg = Box(bar, "seg");
                seg.style.flexGrow = 1;
                seg.style.marginLeft = seg.style.marginRight = 1.5f;
                Radius(seg, 6);
                h.segments[i] = seg;
            }
            Label powerLabel = Text(h.power, cfg.powerGaugeText, titleFont, 22, Color.white);
            powerLabel.style.marginLeft = 12;
            InkShadow(powerLabel, 3);

            // Hint / instruction banner (out of reach, ball in hand, call the 8).
            h.banner = PopCard(h.content, "banner");
            h.banner.style.position = Position.Absolute;
            h.banner.style.left = Length.Percent(50);
            h.banner.style.bottom = Length.Percent(15);
            h.banner.style.translate = new Translate(Length.Percent(-50), 0);
            h.banner.style.paddingLeft = h.banner.style.paddingRight = 20;
            h.banner.style.paddingTop = h.banner.style.paddingBottom = 8;
            h.banner.style.display = DisplayStyle.None;
            h.bannerText = Text(h.banner, "", bodyFont, cfg.bannerFontSize, Ink);

            // Power Wash Pool's clock, top centre (hidden in the other modes).
            h.clock = Text(h.content, "", titleFont, cfg.clockFontSize, Color.white);
            h.clock.style.position = Position.Absolute;
            h.clock.style.left = Length.Percent(50);
            h.clock.style.top = Length.Percent(3);
            h.clock.style.translate = new Translate(Length.Percent(-50), 0);
            h.clock.style.unityTextAlign = TextAnchor.UpperCenter;
            h.clock.style.display = DisplayStyle.None;
            InkShadow(h.clock, 5);

            // Interjections and cards go on top.
            h.fx = Box(h.content, "fx");
            Fill(h.fx);
            return h;
        }

        private void BuildEnd()
        {
            endLayer = Box(root, "end");
            Fill(endLayer);
            endLayer.style.backgroundColor = new Color(0.03f, 0.02f, 0.01f, 0.6f);
            endLayer.style.display = DisplayStyle.None;

            endRays = Box(endLayer, "rays");
            endRays.style.position = Position.Absolute;
            endRays.style.width = endRays.style.height = 1500;
            endRays.style.left = Length.Percent(50);
            endRays.style.top = Length.Percent(30);
            endRays.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            endRays.generateVisualContent += ctx => DrawRays(ctx, new Color(1f, 0.82f, 0.25f, 0.16f));

            for (int i = 0; cfg.showConfetti && i < 50; i++)
            {
                VisualElement c = Box(endLayer, "confetti");
                c.style.position = Position.Absolute;
                c.style.width = 18;
                c.style.height = 9;
                Radius(c, 2);
                Color[] colors = { Yellow, P1, Cream, Yellow, P2 };
                c.style.backgroundColor = colors[i % colors.Length];
                c.style.left = Length.Percent((i * 23) % 100);
                confetti.Add(c);
            }

            endCard = PopCard(endLayer, "winner");
            endCard.style.position = Position.Absolute;
            endCard.style.left = Length.Percent(50);
            endCard.style.top = Length.Percent(30);
            endCard.style.flexDirection = FlexDirection.Row;
            endCard.style.alignItems = Align.Center;
            endCard.style.paddingLeft = 14;
            endCard.style.paddingRight = 40;
            endCard.style.paddingTop = endCard.style.paddingBottom = 14;
            endWhoBox = Box(endCard, "who");
            endWhoBox.style.width = endWhoBox.style.height = 110;
            endWhoBox.style.justifyContent = Justify.Center;
            endWhoBox.style.marginRight = 24;
            Radius(endWhoBox, 20);
            Border(endWhoBox, 4, Ink);
            endWho = Text(endWhoBox, "J1", titleFont, 50, Color.white);
            VisualElement texts = Box(endCard, "texts");
            endTitle = Text(texts, Format(cfg.winnerFormat, 1), titleFont, 76, Ink);
            endTitle.style.unityTextAlign = TextAnchor.MiddleLeft;
            endSubtitle = Text(texts, "", bodyFont, 28, new Color(Ink.r, Ink.g, Ink.b, 0.7f));
            endSubtitle.style.unityTextAlign = TextAnchor.MiddleLeft;

            endButton = PopCard(endLayer, "revanche");
            endButton.style.position = Position.Absolute;
            endButton.style.left = Length.Percent(50);
            endButton.style.top = Length.Percent(62);
            endButton.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            endButton.style.flexDirection = FlexDirection.Row;
            endButton.style.alignItems = Align.Center;
            endButton.style.backgroundColor = Yellow;
            endButton.style.paddingLeft = 12;
            endButton.style.paddingRight = 34;
            endButton.style.paddingTop = endButton.style.paddingBottom = 10;
            endButton.pickingMode = PickingMode.Position;
            VisualElement buttonBall = MiniBall(endButton, 56, 1);
            buttonBall.style.marginRight = 14;
            endButtonText = Text(endButton, cfg.rematchText, titleFont, 44, Ink);
            Label hint = Text(endLayer, cfg.rematchHint, bodyFont, 24, new Color(1f, 1f, 1f, 0.7f));
            hint.style.position = Position.Absolute;
            hint.style.left = Length.Percent(50);
            hint.style.top = Length.Percent(71);
            hint.style.translate = new Translate(Length.Percent(-50), 0);
        }
    }
}
