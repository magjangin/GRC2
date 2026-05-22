using System;
using System.Reflection;
using System.Reflection.Emit;
using GRC2.Harmony.Handlers;
using MelonLoader;

namespace GRC2.Harmony.Registration
{
    internal static class CharactorLoadPatcher
    {
        private static ModuleBuilder _dynamicPatchModule;
        private static int _dynamicPatchCounter = 0;

        private static void EnsureDynamicPatchModule()
        {
            if (_dynamicPatchModule != null) return;
            var asmName = new AssemblyName("GRC2.DynamicPatches");
            var asm = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
            _dynamicPatchModule = asm.DefineDynamicModule("GRC2.DynamicPatches.Module");
        }
        
        public static MethodInfo CreateCharactorPrefixMethodInfo(Type charactorType, Type musicDataType, MethodInfo original)
        {
            try
            {
                EnsureDynamicPatchModule();
                
                int id = System.Threading.Interlocked.Increment(ref _dynamicPatchCounter);
                var tb = _dynamicPatchModule.DefineType(
                    $"GRC2_CharactorPrefixType_{id}",
                    TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
                
                // static void Prefix(ref charactorType __0, musicDataType __1)
                var mb = tb.DefineMethod(
                    "Prefix",
                    MethodAttributes.Public | MethodAttributes.Static,
                    typeof(void),
                    new[] { charactorType.MakeByRefType(), musicDataType });
                
                mb.DefineParameter(1, ParameterAttributes.None, "__0");
                mb.DefineParameter(2, ParameterAttributes.None, "__1");
                
                var il = mb.GetILGenerator();
                
                // locals: object result, object boxedMusic
                var locResult = il.DeclareLocal(typeof(object)); // 0
                var locMusic = il.DeclareLocal(typeof(object));  // 1
                
                // locMusic = (object)__1
                il.Emit(OpCodes.Ldarg_1);
                if (musicDataType.IsValueType)
                    il.Emit(OpCodes.Box, musicDataType);
                il.Emit(OpCodes.Stloc, locMusic);
                
                // result = CharactorLoadPatch.ComputePatchedCharactor(locMusic, typeof(charactorType))
                il.Emit(OpCodes.Ldloc, locMusic);
                il.Emit(OpCodes.Ldtoken, charactorType);
                il.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle", BindingFlags.Public | BindingFlags.Static));
                
                var compute = typeof(CharactorLoadPatch).GetMethod("ComputePatchedCharactor", BindingFlags.Public | BindingFlags.Static);
                if (compute == null)
                    return null;
                
                il.Emit(OpCodes.Call, compute);
                il.Emit(OpCodes.Stloc, locResult);
                
                // if (result == null) return;
                var lblRet = il.DefineLabel();
                il.Emit(OpCodes.Ldloc, locResult);
                il.Emit(OpCodes.Brfalse_S, lblRet);
                
                // write back to ref __0
                il.Emit(OpCodes.Ldarg_0);      // ref charactor
                il.Emit(OpCodes.Ldloc, locResult);
                
                if (charactorType.IsValueType)
                {
                    il.Emit(OpCodes.Unbox_Any, charactorType);
                    il.Emit(OpCodes.Stobj, charactorType);
                }
                else
                {
                    il.Emit(OpCodes.Castclass, charactorType);
                    il.Emit(OpCodes.Stind_Ref);
                }
                
                il.MarkLabel(lblRet);
                il.Emit(OpCodes.Ret);
                
                var created = tb.CreateType();
                return created?.GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[CharactorLoadPatcher] ⚠️ 캐릭터 prefix(TypeBuilder) 생성 실패: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }
    }
}













