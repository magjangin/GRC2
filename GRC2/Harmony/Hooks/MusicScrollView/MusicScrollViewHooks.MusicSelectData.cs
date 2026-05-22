using MelonLoader;
using System;
using System.Reflection;

namespace GRC2.Harmony.Hooks
{
    public static partial class MusicScrollViewHooks
    {
        private static object CreateNewMusicSelectData(MusicScrollInjectContext ctx)
        {
            ConstructorInfo msConstructor = ctx.MsConstructor;

            if (msConstructor != null)
                return CreateMusicSelectDataFromConstructor(ctx, msConstructor);

            return CloneTemplateMusicSelectData(ctx.TemplateMusicSelectData);
        }

        private static object CreateMusicSelectDataFromConstructor(MusicScrollInjectContext ctx, ConstructorInfo msConstructor)
        {
            ParameterInfo[] constructorParams = msConstructor.GetParameters();

            if (constructorParams.Length == 0)
            {
                object newMusicSelectData = msConstructor.Invoke(null);
                CopyMusicSelectDataFieldsFromTemplate(ctx.MusicSelectDataType, ctx.TemplateMusicSelectData, newMusicSelectData);
                return newMusicSelectData;
            }

            if (constructorParams.Length == 1 && constructorParams[0].ParameterType == ctx.MusicSelectDataType)
                return msConstructor.Invoke(new object[] { ctx.TemplateMusicSelectData });

            MelonLogger.Warning($"[MusicScrollViewHooks]   지원하지 않는 생성자 파라미터: {constructorParams.Length}개");
            return null;
        }

        private static object CloneTemplateMusicSelectData(object templateMusicSelectData)
        {
            try
            {
                MethodInfo memberwiseClone = typeof(object).GetMethod("MemberwiseClone",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (memberwiseClone != null)
                {
                    object cloned = memberwiseClone.Invoke(templateMusicSelectData, null);
                    LogVerbose("[MusicScrollViewHooks]   ✅ MemberwiseClone으로 MusicSelectData 복사 성공");
                    return cloned;
                }

                MelonLogger.Warning("[MusicScrollViewHooks]   MemberwiseClone을 찾을 수 없습니다.");
                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MusicScrollViewHooks]   MemberwiseClone 실패: {ex.Message}");
                return null;
            }
        }

        private static void CopyMusicSelectDataFieldsFromTemplate(
            Type musicSelectDataType,
            object templateMusicSelectData,
            object newMusicSelectData)
        {
            FieldInfo[] msFields = musicSelectDataType.GetFields(InstanceMemberFlags);
            foreach (var field in msFields)
            {
                try
                {
                    object value = field.GetValue(templateMusicSelectData);
                    field.SetValue(newMusicSelectData, value);
                }
                catch (Exception)
                {
                    // 필드 단위 복사 불가(읽기 전용 등) 시 스킵
                }
            }
        }
    }
}
