﻿using Stratis.SmartContracts;

public struct WhiteListEntry
{
    public UInt256 CodeHash;
    public Address LastAddress;
    public string Name;
}

public class SystemContractsDictionary : SmartContract
{
    public SystemContractsDictionary(ISmartContractState state) : base(state)
    {
    }

    public bool IsWhiteListed(UInt256 codeHash)
    {
        Assert(codeHash != default(UInt256));

        WhiteListEntry whiteListEntry = this.State.GetStruct<WhiteListEntry>(codeHash.ToString());

        return whiteListEntry.CodeHash != default(UInt256);
    }

    public UInt256 GetCodeHash(string name)
    {
        Assert(!string.IsNullOrEmpty(name));

        return this.State.GetUInt256($"ByName:{name}");
    }

    public Address GetContractAddress(string name)
    {
        Assert(!string.IsNullOrEmpty(name));

        UInt256 codeHash = this.State.GetUInt256($"ByName:{name}");

        if (codeHash == default(UInt256))
            return default(Address);

        WhiteListEntry whiteListEntry = this.State.GetStruct<WhiteListEntry>(codeHash.ToString());

        return whiteListEntry.LastAddress;
    }

    public Address GetContractAddress(UInt256 codeHash)
    {
        Assert(codeHash != default(UInt256));

        WhiteListEntry whiteListEntry = this.State.GetStruct<WhiteListEntry>(codeHash.ToString());

        return whiteListEntry.LastAddress;
    }

    public void WhiteList(byte[] signatures, UInt256 codeHash, Address lastAddress, string name)
    {
        Assert(signatures != null && signatures.Length > 0);
        Assert(codeHash != default(UInt256));

        UInt256 codeHashKey = codeHash;
        WhiteListEntry whiteListEntry;

        if (!string.IsNullOrEmpty(name))
        {
            codeHashKey = this.State.GetUInt256($"ByName:{name}");

            if (codeHashKey == default(UInt256))
                codeHashKey = codeHash;
        }

        whiteListEntry = this.State.GetStruct<WhiteListEntry>(codeHashKey.ToString());

        string message;
        if (whiteListEntry.CodeHash == default(UInt256))
            message = $"WhiteList(CodeHash:{codeHash},LastAddress:{lastAddress},Name:{name})";
        else
            message = $"WhiteList(CodeHash:{whiteListEntry.CodeHash}=>{codeHash},LastAddress:{whiteListEntry.LastAddress}=>{lastAddress},Name:{whiteListEntry.Name}=>{name})";

        //this.VerifySignatures(message, signatures);

        if (whiteListEntry.CodeHash != default(UInt256))
        {
            if (codeHash != whiteListEntry.CodeHash)
                this.State.Clear(whiteListEntry.CodeHash.ToString());

            if (string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(whiteListEntry.Name))
                this.State.Clear($"ByName:{whiteListEntry.Name}");
        }

        whiteListEntry.CodeHash = codeHash;
        whiteListEntry.LastAddress = lastAddress;
        whiteListEntry.Name = name;

        this.State.SetStruct<WhiteListEntry>(codeHash.ToString(), whiteListEntry);
        this.State.SetUInt256($"ByName:{name}", codeHash);
    }

    public void BlackList(byte[] signatures, UInt256 codeHash)
    {
        Assert(signatures != null && signatures.Length > 0);
        Assert(codeHash != default(UInt256));

        WhiteListEntry whiteListEntry = this.State.GetStruct<WhiteListEntry>(codeHash.ToString());
        
        Assert(whiteListEntry.CodeHash != default(UInt256));

        string message = $"BlackList(CodeHash:{whiteListEntry.CodeHash},LastAddress:{whiteListEntry.LastAddress},Name:{whiteListEntry.Name})";

        //this.VerifySignatures(message, signatures);

        this.State.Clear(codeHash.ToString());
        this.State.Clear($"ByName:{whiteListEntry.Name}");
    }
}