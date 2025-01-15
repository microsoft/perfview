
extern EnterMethod:proc
extern CallSampleCount:dword

_TEXT segment para 'CODE'

;************************************************************************************
;typedef void EnterMethodNaked(
;         rcx = FunctionIDOrClientID functionIDOrClientID);

        align   16
        public  EnterMethodNaked
EnterMethodNaked     proc    frame
		.endprolog
		lock dec [CallSampleCount]
		jle	   EnterMethodSampleNaked
		ret

EnterMethodNaked     endp

;************************************************************************************
; This is a helper that does work if we are actually taking a call sample
;typedef void EnterMethodSampleNaked(
;         rcx = FunctionIDOrClientID functionIDOrClientID);

        align   16
		public  EnterMethodSampleNaked
EnterMethodSampleNaked     proc    frame
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

EnterMethodSampleNaked     endp


;************************************************************************************
;typedef void TailcallMethodNaked(
;         rcx = FunctionIDOrClientID functionIDOrClientID);

        align   16
        public  TailcallMethodNaked
TailcallMethodNaked  proc    frame
	    .endprolog
		jmp EnterMethodNaked

TailcallMethodNaked  endp

;************************************************************************************
_TEXT ends
end
