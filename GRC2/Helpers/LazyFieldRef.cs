using System;
using HarmonyLib;
using MelonLoader;

namespace GRC2.Helpers
{
    /// <summary>
    /// 게임 필드에 이름으로 접근하는 <see cref="AccessTools.FieldRef{T, F}"/>를 처음 쓸 때 만듭니다.
    ///
    /// 예전에는 패치 클래스의 <c>static readonly</c> 필드 초기화에서 바로 <c>FieldRefAccess</c>를 불렀습니다.
    /// 게임 업데이트로 필드 하나가 사라지거나 형식이 바뀌면 그 클래스의 정적 초기화가 던지고
    /// (<see cref="TypeInitializationException"/>), 같은 클래스의 다른 필드·훅까지 그 실행 내내 전부 멈췄습니다.
    /// 지금은 사라진 필드를 쓰는 코드 경로만 예외를 받고(호출부의 try가 잡음), 경고는 필드마다 한 번만 남깁니다.
    ///
    /// 소스에서 <c>new LazyFieldRef&lt;…&gt;("이름")</c>로 쓴 이름은 <c>GameCompatibilityTests</c>가
    /// <c>GameCompatibility.FieldContracts</c>와 대조하므로, 새 필드를 쓰면 그 표에도 한 줄 넣어야 합니다.
    /// </summary>
    public sealed class LazyFieldRef<T, F> where T : class
    {
        private readonly string _fieldName;
        private AccessTools.FieldRef<T, F> _ref;
        private Exception _error;

        public LazyFieldRef(string fieldName)
        {
            _fieldName = fieldName;
        }

        public string FieldName => _fieldName;

        /// <summary>필드를 찾았는지 확인합니다(처음 부를 때 조회). 못 찾으면 경고를 한 번 남기고 false입니다.</summary>
        public bool IsAvailable => TryResolve() != null;

        /// <summary>
        /// 인스턴스의 필드를 ref로 돌려줍니다. 읽기(<c>x = Ref[obj]</c>)와 쓰기(<c>Ref[obj] = x</c>) 모두 됩니다.
        /// 필드를 찾지 못했으면 <see cref="MissingFieldException"/>을 던집니다.
        /// </summary>
        public ref F this[T instance]
        {
            get
            {
                var fieldRef = TryResolve();
                if (fieldRef == null)
                    throw new MissingFieldException(typeof(T).FullName, _fieldName);
                return ref fieldRef(instance);
            }
        }

        private AccessTools.FieldRef<T, F> TryResolve()
        {
            if (_ref != null)
                return _ref;
            if (_error != null)
                return null;

            try
            {
                _ref = AccessTools.FieldRefAccess<T, F>(_fieldName);
                return _ref;
            }
            catch (Exception ex)
            {
                _error = ex;
                MelonLogger.Warning(
                    $"[호환성] {typeof(T).Name}.{_fieldName} 필드를 찾지 못해 이 필드를 쓰는 기능만 꺼집니다 " +
                    $"(게임 업데이트 의심, 업데이트_대응_가이드.md 참고): {ex.Message}");
                return null;
            }
        }
    }
}
