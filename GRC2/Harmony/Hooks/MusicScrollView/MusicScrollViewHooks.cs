using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using GRC2.Core;
using GRC2.Helpers;

namespace GRC2.Harmony.Hooks
{
    /// <summary>
    /// cMusicSelectScrollView 및 cMusicSelectScrollViewItem 메서드 후킹
    /// </summary>
    public static partial class MusicScrollViewHooks
    {
        // 상세 목록/리플렉션 로그는 기본 비활성화해서 런타임 로그 노이즈를 줄입니다.
        private static bool IsVerboseLoggingEnabled => false;

        private static void LogVerbose(string message)
        {
            if (IsVerboseLoggingEnabled)
            {
                MelonLogger.Msg(message);
            }
        }
    }
}

