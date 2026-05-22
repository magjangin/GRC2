using System;
using System.Text;
using MelonLoader;

namespace GRC2.Helpers
{
    /// <summary>
    /// 일관된 에러 로깅을 위한 헬퍼 클래스
    /// </summary>
    public static class ErrorLogger
    {
        /// <summary>
        /// 예외를 표준 형식으로 로깅합니다.
        /// </summary>
        /// <param name="ex">로깅할 예외</param>
        /// <param name="context">컨텍스트 정보 (예: "[ClassName] MethodName")</param>
        /// <param name="additionalMessage">추가 메시지 (선택사항)</param>
        public static void LogException(Exception ex, string context, string additionalMessage = null)
        {
            if (ex == null)
            {
                MelonLogger.Error($"{context} 예외가 null입니다.");
                return;
            }

            var messageBuilder = new StringBuilder();
            
            // 컨텍스트 정보
            if (!string.IsNullOrEmpty(context))
            {
                messageBuilder.Append($"{context} ");
            }
            
            // 추가 메시지
            if (!string.IsNullOrEmpty(additionalMessage))
            {
                messageBuilder.Append($"{additionalMessage}: ");
            }
            
            // 예외 메시지
            messageBuilder.Append($"오류: {ex.Message}");
            
            // InnerException 처리
            if (ex.InnerException != null)
            {
                messageBuilder.Append($" (내부 예외: {ex.InnerException.Message})");
            }
            
            MelonLogger.Error(messageBuilder.ToString());
            
            // 스택 트레이스
            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                MelonLogger.Error(ex.StackTrace);
            }
        }

        /// <summary>
        /// 예외를 경고 레벨로 로깅합니다 (치명적이지 않은 오류용).
        /// </summary>
        /// <param name="ex">로깅할 예외</param>
        /// <param name="context">컨텍스트 정보 (예: "[ClassName] MethodName")</param>
        /// <param name="additionalMessage">추가 메시지 (선택사항)</param>
        public static void LogWarning(Exception ex, string context, string additionalMessage = null)
        {
            if (ex == null)
            {
                MelonLogger.Warning($"{context} 예외가 null입니다.");
                return;
            }

            var messageBuilder = new StringBuilder();
            
            // 컨텍스트 정보
            if (!string.IsNullOrEmpty(context))
            {
                messageBuilder.Append($"{context} ");
            }
            
            // 추가 메시지
            if (!string.IsNullOrEmpty(additionalMessage))
            {
                messageBuilder.Append($"{additionalMessage}: ");
            }
            
            // 예외 메시지
            messageBuilder.Append($"경고: {ex.Message}");
            
            // InnerException 처리
            if (ex.InnerException != null)
            {
                messageBuilder.Append($" (내부 예외: {ex.InnerException.Message})");
            }
            
            MelonLogger.Warning(messageBuilder.ToString());
        }
    }
}
