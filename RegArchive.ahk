#Requires AutoHotkey v2.0.0+
;==============================================================
; RegArchive — Registry import/export helper using reg.exe with optional RegView selection
;
; GitHub: https://github.com/SevenKeyboard/reg-archive
; Author: SevenKeyboard Ltd. (2026)
; License: The Unlicense
;==============================================================
class VersionManager_RegArchive
{
    static _ := this._init()
    static _init()    {
        global
        REGARCHIVE_VERSION := "1.0.0"
    }
}
class RegArchive
{
    static import(filePath, regView := A_RegView)    {
        if (filePath == '')
            return false
        if (inStr(filePath, '"'))
            return false
        fileAttr := fileExist(filePath)
        if (!fileAttr)
            return false
        if (inStr(fileAttr, 'D'))
            return false
        command := '"' . this._getRegExePath() . '" import'
        command .= ' "' . filePath . '"'
        if (regView == 32 || regView == 64)
            command .= ' /reg:' . format('{:d}', regView)
        try exitCode := runWait(command,, 'Hide')
        catch  {
            return false
        }
        return (exitCode == 0)
    }
    static export(keyPath, filePath, overwrite := true, regView := A_RegView)    {
        if (keyPath == '' || filePath == '')
            return false
        if (inStr(keyPath, '"') || inStr(filePath, '"'))
            return false
        splitPath(filePath,, &dir)
        if (dir == '')
            return false
        if (!inStr(fileExist(dir), 'D'))    {
            try dirCreate(dir)
            catch  {
                return false
            }
        }
        command := '"' . this._getRegExePath() . '" export'
        command .= ' "' . keyPath  . '"'
        command .= ' "' . filePath .  '"'
        if (overwrite)
            command .= ' /y'
        if (regView == 32 || regView == 64)
            command .= ' /reg:' . format('{:d}', regView)
        try exitCode := runWait(command,, 'Hide')
        catch  {
            return false
        }
        return (exitCode == 0)
    }
    static _getRegExePath()    {
        winDir := A_WinDir
        if (A_Is64bitOS && A_PtrSize == 4 && fileExist(winDir . "\Sysnative\reg.exe"))
            return winDir . "\Sysnative\reg.exe"
        return winDir . "\System32\reg.exe"
    }
}