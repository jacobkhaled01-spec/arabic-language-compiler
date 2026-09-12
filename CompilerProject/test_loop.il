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
    .locals init ([0] int32 v_0, [1] int32 v_1, [2] int32 v_2, [3] int32 v_3)

    ldc.i4 0
    stloc 1
    ldc.i4 1
    stloc 2
  L1:
    ldloc 2
    ldc.i4 4
    ble L2
    br L3
  L2:
    ldloc 1
    ldloc 2
    add
    stloc 3
    ldloc 3
    stloc 1
    ldloc 2
    ldc.i4 1
    add
    stloc 2
    br L1
  L3:
    ldloc 1
    call void [mscorlib]System.Console::WriteLine(int32)
    ret
  }
}
