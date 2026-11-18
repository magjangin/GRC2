using System;
using System.Collections.Generic;
using GRC2.Helpers;
using MelonLoader;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GRC2.Core
{
    /// <summary>
    /// TMP 글자가 보이는 칸보다 길면 옆으로 흘려 끝까지 보여 줍니다(곡 선택 화면에서 선택한 곡 제목에 붙입니다).
    ///
    /// 게임은 긴 제목을 칸 끝에서 그냥 잘랐습니다. 게임에도 cUICommonScrollText라는 스크롤 컴포넌트가 있지만
    /// Update가 비어 있어 아무것도 하지 않습니다.
    ///
    /// 방식: 글자가 넘칠 때만 TMP를 "넘침 허용·줄바꿈 없음·왼쪽 정렬"로 두고, 왼쪽 margin을 음수로 줄여 글자만
    /// 왼쪽으로 민다. RectTransform을 움직이지 않으므로 게임의 열기·닫기 애니메이션이나 오브젝트 구조와 부딪히지 않는다.
    /// 원래 TMP가 스스로 자르고 있었다면(넘침 모드가 Overflow가 아니면) 같은 칸에 RectMask2D를 붙여 칸 밖을 가린다.
    /// RectMask2D는 위아래로 칸 높이만큼 넓혀 두어 글자의 위아래가 잘리지 않게 한다.
    /// 제목이 바뀌면 원래 설정으로 돌려놓은 뒤 다시 잰다.
    /// </summary>
    public class TextMarquee : MonoBehaviour
    {
        /// <summary>끝 글자 뒤에 남길 여유(로컬 단위).</summary>
        private const float EndPadding = 12f;

        private TextMeshProUGUI _text;
        private RectTransform _rect;
        private RectMask2D _selfMask;
        private readonly MarqueeTimeline _timeline = new MarqueeTimeline();
        private readonly Vector3[] _corners = new Vector3[4];

        private bool _captured;
        private TextOverflowModes _origOverflow;
        private bool _origWrap;
        private HorizontalAlignmentOptions _origAlign;
        private Vector4 _origMargin;

        private string _lastText;
        private float _lastWidth = -1f;
        private bool _scrolling;
        private float _appliedOffset;

        /// <summary>대상 TMP에 붙입니다(이미 있으면 그대로).</summary>
        public static void Attach(TextMeshProUGUI text)
        {
            if (text == null || text.GetComponent<TextMarquee>() != null)
                return;
            text.gameObject.AddComponent<TextMarquee>();
        }

        private void Awake()
        {
            _text = GetComponent<TextMeshProUGUI>();
            _rect = GetComponent<RectTransform>();
        }

        private void OnDisable()
        {
            // 다시 켜질 때 처음부터 다시 잽니다.
            Restore();
            _lastText = null;
        }

        private void LateUpdate()
        {
            if (_text == null || _rect == null)
                return;

            try
            {
                float width = _rect.rect.width;
                if (!string.Equals(_text.text, _lastText, StringComparison.Ordinal) || Mathf.Abs(width - _lastWidth) > 0.5f)
                {
                    _lastText = _text.text;
                    _lastWidth = width;
                    Measure();
                }

                if (!_scrolling)
                    return;

                float offset = _timeline.Advance(Time.unscaledDeltaTime);
                if (Mathf.Abs(offset - _appliedOffset) > 0.01f)
                    ApplyOffset(offset);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[제목 스크롤] 중단: {ex.Message}");
                Restore();
                enabled = false;
            }
        }

        private void Capture()
        {
            if (_captured)
                return;
            _origOverflow = _text.overflowMode;
            _origWrap = _text.enableWordWrapping;
            _origAlign = _text.horizontalAlignment;
            _origMargin = _text.margin;
            _captured = true;
        }

        private void Restore()
        {
            _scrolling = false;
            _appliedOffset = 0f;
            if (_selfMask != null)
                _selfMask.enabled = false;
            if (!_captured || _text == null)
                return;
            _text.overflowMode = _origOverflow;
            _text.enableWordWrapping = _origWrap;
            _text.horizontalAlignment = _origAlign;
            _text.margin = _origMargin;
        }

        private void Measure()
        {
            Capture();
            Restore();

            if (string.IsNullOrEmpty(_text.text))
                return;

            // 자르지 않았을 때의 실제 글자 폭을 잽니다.
            _text.overflowMode = TextOverflowModes.Overflow;
            _text.enableWordWrapping = false;
            _text.horizontalAlignment = HorizontalAlignmentOptions.Left;
            _text.ForceMeshUpdate();
            Bounds bounds = _text.textBounds;
            float textRight = bounds.max.x;

            bool selfClipped = _origOverflow != TextOverflowModes.Overflow;
            float ancestorRight;
            string ancestorName = FindAncestorClipRight(out ancestorRight);

            float visibleRight = float.PositiveInfinity;
            if (selfClipped)
                visibleRight = _rect.rect.xMax;
            if (ancestorName != null)
                visibleRight = Mathf.Min(visibleRight, ancestorRight);

            float distance = float.IsInfinity(visibleRight) ? 0f : textRight - visibleRight + EndPadding;
            bool overflowing = distance > EndPadding + 0.5f;

            ModLog.InfoOnChange("text-marquee",
                $"[제목 스크롤] 대상 {_text.gameObject.name}: 넘침 모드 {_origOverflow}, 정렬 {_origAlign}, 줄바꿈 {_origWrap}, " +
                $"자동 크기 {_text.enableAutoSizing}, 칸 폭 {_rect.rect.width:0}, 가리는 부모 {ancestorName ?? "없음"}" +
                (GetComponent("cUICommonScrollText") != null ? ", 게임 cUICommonScrollText 있음" : ""));

            if (!overflowing)
            {
                Restore();
                return;
            }

            if (selfClipped)
            {
                if (_selfMask == null)
                {
                    _selfMask = gameObject.GetComponent<RectMask2D>();
                    if (_selfMask == null)
                        _selfMask = gameObject.AddComponent<RectMask2D>();
                }
                float h = _rect.rect.height;
                _selfMask.padding = new Vector4(0f, -h, 0f, -h);
                _selfMask.enabled = true;
            }

            _timeline.Reset(distance);
            _appliedOffset = 0f;
            _scrolling = true;
            ModLog.Verbose($"[제목 스크롤] '{_text.text}' 이동 거리 {distance:0}");
        }

        /// <summary>이 텍스트를 가리는 가장 가까운 부모 마스크의 오른쪽 끝(이 텍스트의 로컬 x)을 찾습니다. 없으면 null.</summary>
        private string FindAncestorClipRight(out float right)
        {
            right = float.PositiveInfinity;
            string name = null;
            for (Transform t = transform.parent; t != null; t = t.parent)
            {
                RectTransform rt = t as RectTransform;
                if (rt == null)
                    continue;

                RectMask2D rectMask = t.GetComponent<RectMask2D>();
                Mask mask = t.GetComponent<Mask>();
                bool clips = (rectMask != null && rectMask.isActiveAndEnabled) || (mask != null && mask.isActiveAndEnabled);
                if (!clips)
                    continue;

                rt.GetWorldCorners(_corners);
                float localRight = Mathf.Max(
                    _rect.InverseTransformPoint(_corners[2]).x,
                    _rect.InverseTransformPoint(_corners[3]).x);
                if (localRight < right)
                {
                    right = localRight;
                    name = t.name;
                }
            }
            return name;
        }

        private void ApplyOffset(float offset)
        {
            _appliedOffset = offset;
            Vector4 m = _origMargin;
            m.x -= offset;
            _text.margin = m;
        }
    }
}
