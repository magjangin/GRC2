using System;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace GRC2.Injectors
{
    /// <summary>
    /// BGM Injector 후킹에서 사용하는 포맷팅 유틸리티 클래스
    /// </summary>
    internal static partial class BgmFormattingUtils
    {
        public static string FormatArguments(object[] args)
        {
            if (args == null || args.Length == 0)
            {
                return "";
            }
            
            var argValues = new List<string>();
            foreach (var arg in args)
            {
                if (arg is AudioClip clip)
                {
                    var clipName = string.IsNullOrEmpty(clip.name) ? "(이름 없음)" : clip.name;
                    argValues.Add($"AudioClip({clipName}, {clip.length:F3}초)");
                }
                else if (arg is float f)
                    argValues.Add($"{f:F3}");
                else if (arg is int n)
                    argValues.Add(n.ToString());
                else if (arg is string s)
                    argValues.Add($"\"{s}\"");
                else if (arg is bool b)
                    argValues.Add(b.ToString());
                else if (arg != null)
                    argValues.Add(arg.ToString());
                else
                    argValues.Add("null");
            }
            return string.Join(", ", argValues);
        }
        
        public static string FormatResult(object result)
        {
            if (result == null)
            {
                return "";
            }
            
            if (result is float rf)
                return $" → {rf:F3}";
            else if (result is int ri)
                return $" → {ri}";
            else if (result is AudioClip resultClip)
            {
                var resultClipName = string.IsNullOrEmpty(resultClip.name) ? "(이름 없음)" : resultClip.name;
                return $" → AudioClip({resultClipName}, {resultClip.length:F3}초)";
            }
            else
                return $" → {result}";
        }
    }
}

