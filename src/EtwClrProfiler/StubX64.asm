
extern EnterMethod:proc
extern TailcallMethod:proc

_TEXT segment para 'CODE'

;************************************************************************************
;typedef void EnterMethodNaked(
;         rcx = FunctionIDOrClientID functionIDOrClientID);

        align   16
        public  EnterMethodNaked

EnterMethodNaked     proc    frame

        ; save registers
        push    rax
        .allocstack 8

        push    r10
        .allocstack 8

        push    r11
        .allocstack 8

        sub     rsp, 20h
        .allocstack 20h

        .endprolog

        call    EnterMethod

        add     rsp, 20h

        ; restore registers
        pop     r11
        pop     r10
        pop     rax

        ; return
        ret

EnterMethodNaked     endp

;************************************************************************************
;typedef void LeaveMethodNaked(
;         rcx = FunctionIDOrClientID functionIDOrClientID);

        align   16

        public  LeaveMethodNaked

LeaveMethodNaked     proc    frame
	    .endprolog
        ; we dont care about leaving methods, do nothing.  
        ret

LeaveMethodNaked     endp

;************************************************************************************
;typedef void TailcallMethodNaked(
;         rcx = FunctionIDOrClientID functionIDOrClientID);

        align   16
        public  TailcallMethodNaked

TailcallMethodNaked  proc    frame

        ; save rax
        push    rax
        .allocstack 8

        sub     rsp, 20h
        .allocstack 20h

        .endprolog

        call    TailcallMethod

        add     rsp, 20h

        ; restore rax
        pop     rax

        ; return
        ret

TailcallMethodNaked  endp

;************************************************************************************
_TEXT ends
end
