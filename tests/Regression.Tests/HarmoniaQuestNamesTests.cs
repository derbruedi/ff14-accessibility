using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FF14Accessibility.Services;

namespace Regression.Tests;

public sealed class HarmoniaQuestNamesTests
{
    private static readonly byte[] Original = Encoding.UTF8.GetBytes("A Hearer Is Often Late");

    private static (byte[] Bytes,string Hash) Pack(string target="Новое название задания",string language="ru",ulong? guard=null)
    {
        var sections=new List<byte[]> {
            JsonSerializer.SerializeToUtf8Bytes(new{language,game=new{language="en"}}),
            Encoding.UTF8.GetBytes("Quest"), new byte[32],new byte[8],new byte[16],new byte[24],
            Encoding.UTF8.GetBytes(target+'\0')};
        void U32(byte[] bytes,int at,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at,4),value);
        U32(sections[2],4,5); U32(sections[2],16,1); U32(sections[2],24,1);
        U32(sections[4],0,65920); BinaryPrimitives.WriteUInt16LittleEndian(sections[4].AsSpan(6,2),1);
        U32(sections[5],4,(uint)(sections[6].Length-1));
        BinaryPrimitives.WriteUInt64LittleEndian(sections[5].AsSpan(16,8),guard??HarmoniaQuestNames.Guard(Original));
        var offsets=new List<int>();var end=64+24*7;
        foreach(var section in sections){end=(end+7)&~7;offsets.Add(end);end+=section.Length;}
        end=(end+7)&~7;var bytes=new byte[end+32];"AERIAHPK"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8,2),1); U32(bytes,12,64);U32(bytes,24,7);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(16,8),(ulong)bytes.Length);
        for(var i=0;i<7;i++) {var at=64+i*24;U32(bytes,at,(uint)i+1);
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(at+8,8),(ulong)offsets[i]);
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(at+16,8),(ulong)sections[i].Length);
            sections[i].CopyTo(bytes,offsets[i]);}
        var hash=SHA256.HashData(bytes.AsSpan(0,end));hash.CopyTo(bytes,end);
        return(bytes,Convert.ToHexString(hash));
    }

    [Fact]
    public void NewTranslatedNameKeepsItsActualQuestId()
    {
        var (bytes,hash)=Pack();using var stream=new MemoryStream(bytes);
        var row=Assert.Single(HarmoniaQuestNames.Read(stream,hash,id=>id==65920?Original:null));
        Assert.Equal(65920u,row.RowId); Assert.Equal("Новое название задания",row.Name);
    }
    [Theory]
    [InlineData("A different quest")] [InlineData("")]
    public void ChangedOriginalNameCannotBorrowAnOldTranslation(string changed)
    {
        var (bytes,hash)=Pack();using var stream=new MemoryStream(bytes);
        Assert.Empty(HarmoniaQuestNames.Read(stream,hash,_=>Encoding.UTF8.GetBytes(changed)));
    }
    [Fact]
    public void MissingRowAndWrongSourceGuardStayUnknown()
    {
        var (bytes,hash)=Pack();using var stream=new MemoryStream(bytes);
        Assert.Empty(HarmoniaQuestNames.Read(stream,hash,_=>null));
        (bytes,hash)=Pack(guard:123);using var wrong=new MemoryStream(bytes);
        Assert.Empty(HarmoniaQuestNames.Read(wrong,hash,_=>Original));
    }
    [Fact]
    public void OtherLanguageIsNotImportedAsRussian()
    {
        var (bytes,hash)=Pack(language:"fr");using var stream=new MemoryStream(bytes);
        Assert.Empty(HarmoniaQuestNames.Read(stream,hash,_=>Original));
    }
    [Fact]
    public void TruncatedCorruptedAndWrongInstalledPackCannotSupplyQuestNames()
    {
        var (bytes,hash)=Pack();using var shortStream=new MemoryStream(bytes[..80]);
        Assert.Throws<InvalidDataException>(()=>HarmoniaQuestNames.Read(shortStream,hash,_=>Original));
        using var wrong=new MemoryStream(bytes);
        Assert.Throws<InvalidDataException>(()=>HarmoniaQuestNames.Read(wrong,new string('0',64),_=>Original));
        bytes[220]^=1;using var corrupt=new MemoryStream(bytes);
        Assert.Throws<InvalidDataException>(()=>HarmoniaQuestNames.Read(corrupt,hash,_=>Original));
    }
    [Theory]
    [InlineData(0,QuestKind.MainStory)] [InlineData(1,QuestKind.MainStory)]
    [InlineData(3,QuestKind.SideQuest)] [InlineData(6,QuestKind.Job)] [InlineData(99,QuestKind.Unknown)]
    public void KindStillComesFromGameJournalSection(uint section,QuestKind expected)
        =>Assert.Equal(expected,QuestMarkerService.KindForSection(section));
}
