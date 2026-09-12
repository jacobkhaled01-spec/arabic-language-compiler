// ===================================================
// كود .NET Common Intermediate Language (CIL) المولد
// ===================================================
.assembly extern mscorlib {}
.assembly ArabicProgram {}
.module ArabicProgram.exe

.class public auto ansi beforefieldinit Program extends [mscorlib]System.Object
{
  .method public static void Main() cil managed
  {
    .entrypoint
    .maxstack 64
    .locals init ([0] int32 v_0, [1] int32 v_1, [2] int32 v_2)

    ldc.i4 20
    stloc 0
    ldc.i4 30
    stloc 1
    ldloc 0
    ldloc 1
    add
    stloc 2
    ldloc 2
    call void [mscorlib]System.Console::WriteLine(int32)
    ret
  }
}
