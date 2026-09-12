; ===================================================
; كود لغة التجميع x86 Assembly المولد من المترجم العربي
; ===================================================
.386
.model flat, stdcall
option casemap :none

.data
    v_0 DD 0
    v_1 DD 0
    v_2 DD 0

.code
main PROC
    MOV EAX, 20
    MOV v_0, EAX
    MOV EAX, 30
    MOV v_1, EAX
    MOV EAX, v_0
    ADD EAX, v_1
    MOV v_2, EAX
    RET
main ENDP
END main
