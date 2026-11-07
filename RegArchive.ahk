#Requires AutoHotkey v1.1.17+
;==============================================================
; RegArchive — Registry import/export helper using reg.exe with optional RegView selection
;
; GitHub: https://github.com/SevenKeyboard/reg-archive
; Author: SevenKeyboard Ltd. (2026)
; License: The Unlicense
;==============================================================
class VersionManager_RegArchive
{
    static _ := VersionManager_RegArchive._init()
    _init()    {
        global
        REGARCHIVE_VERSION := "1.0.1"
    }
}
class RegArchive
{
    import(filePath, regView := "")    {
        if (filePath == "")
            return false
        if (inStr(filePath, """"))
            return false
        fileAttr := fileExist(filePath)
        if (!fileAttr)
            return false
        if (inStr(fileAttr, "D"))
            return false
        command := """" . this._getRegExePath() . """ import"
        command .= " """ . filePath . """"
        if (regView == "")
            regView := A_RegView
        if (regView == 32 || regView == 64)
            command .= " /reg:" . format("{:d}", regView)
        prevErrorLevel := errorLevel
        runWait % command,, % "Hide UseErrorLevel"
        result := (errorLevel == 0)
        errorLevel := prevErrorLevel
        return result
    }
    export(keyPath, filePath, overwrite := true, regView := "")    {
        if (keyPath == "" || filePath == "")
            return false
        if (inStr(keyPath, """") || inStr(filePath, """"))
            return false
        if (!overwrite && fileExist(filePath))
            return false
        splitPath, filePath,, dir
        if (dir == "")
            return false
        if (!inStr(fileExist(dir), "D"))    {
            prevErrorLevel := errorLevel
            fileCreateDir % dir
            ok := !errorLevel
            errorLevel := prevErrorLevel
            if (!ok)
                return false
        }
        command := """" . this._getRegExePath() . """ export"
        command .= " """ . keyPath  . """"
        command .= " """ . filePath .  """"
        if (overwrite)
            command .= " /y"
        if (regView == "")
            regView := A_RegView
        if (regView == 32 || regView == 64)
            command .= " /reg:" . format("{:d}", regView)
        prevErrorLevel := errorLevel
        runWait % command,, % "Hide UseErrorLevel"
        result := (errorLevel == 0)
        errorLevel := prevErrorLevel
        return result
    }
    _getRegExePath()    {
        winDir := A_WinDir
        if (A_Is64bitOS && A_PtrSize == 4 && fileExist(winDir . "\Sysnative\reg.exe"))
            return winDir . "\Sysnative\reg.exe"
        return winDir . "\System32\reg.exe"
    }
}