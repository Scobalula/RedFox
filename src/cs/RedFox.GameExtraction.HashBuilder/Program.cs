// See https://aka.ms/new-console-template for more information

using RedFox.GameExtraction.Hashing;

var nameTable = new NameTable();

nameTable.HashCount = 4838281;
nameTable.HashAlgorithm = "MurMur3";

NameFile.Save("Test.namefile", nameTable);
