﻿using System;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Patricia;
using Stratis.SmartContracts.CLR.Caching;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.Loader;
using Stratis.SmartContracts.CLR.ResultProcessors;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.CLR.Validation;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Networks;
using Xunit;


namespace Stratis.SmartContracts.CLR.Tests
{
    public class SystemContractDictionaryTests
    {
        private const ulong BlockHeight = 0;
        private static readonly uint160 CoinbaseAddress = 0;
        private static readonly uint160 ToAddress = 1;
        private static readonly uint160 SenderAddress = 2;
        private static readonly Money MempoolFee = new Money(1_000_000);
        private readonly IKeyEncodingStrategy keyEncodingStrategy;
        private readonly ILoggerFactory loggerFactory;
        private readonly Network network;
        private readonly IContractRefundProcessor refundProcessor;
        private readonly IStateRepositoryRoot state;
        private readonly IContractTransferProcessor transferProcessor;
        private readonly SmartContractValidator validator;
        private IInternalExecutorFactory internalTxExecutorFactory;
        private readonly IContractAssemblyCache contractCache;
        private IVirtualMachine vm;
        private readonly ICallDataSerializer callDataSerializer;
        private readonly StateFactory stateFactory;
        private readonly IAddressGenerator addressGenerator;
        private readonly ILoader assemblyLoader;
        private readonly IContractModuleDefinitionReader moduleDefinitionReader;
        private readonly IContractPrimitiveSerializer contractPrimitiveSerializer;
        private readonly IStateProcessor stateProcessor;
        private readonly ISmartContractStateFactory smartContractStateFactory;
        private readonly ISerializer serializer;

        public SystemContractDictionaryTests()
        {
            this.keyEncodingStrategy = BasicKeyEncodingStrategy.Default;
            this.loggerFactory = ExtendedLoggerFactory.Create();
            this.network = new SmartContractsRegTest();
            this.refundProcessor = new ContractRefundProcessor(this.loggerFactory);
            this.state = new StateRepositoryRoot(new NoDeleteSource<byte[], byte[]>(new MemoryDictionarySource()));
            this.transferProcessor = new ContractTransferProcessor(this.loggerFactory, this.network);
            this.validator = new SmartContractValidator();
            this.addressGenerator = new AddressGenerator();
            this.assemblyLoader = new ContractAssemblyLoader();
            this.moduleDefinitionReader = new ContractModuleDefinitionReader();
            this.contractPrimitiveSerializer = new ContractPrimitiveSerializer(this.network);
            this.serializer = new Serializer(this.contractPrimitiveSerializer);
            this.contractCache = new ContractAssemblyCache();
            this.vm = new ReflectionVirtualMachine(this.validator, this.loggerFactory, this.assemblyLoader, this.moduleDefinitionReader, this.contractCache);
            this.stateProcessor = new StateProcessor(this.vm, this.addressGenerator);
            this.internalTxExecutorFactory = new InternalExecutorFactory(this.loggerFactory, this.stateProcessor);
            this.smartContractStateFactory = new SmartContractStateFactory(this.contractPrimitiveSerializer, this.internalTxExecutorFactory, this.serializer);

            this.callDataSerializer = new CallDataSerializer(this.contractPrimitiveSerializer);

            this.stateFactory = new StateFactory(this.smartContractStateFactory);
        }

        [Fact]
        public void CanCompileSystemContractDictionary()
        {
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/SystemContractsDictionary.cs");
            Assert.True(compilationResult.Success);
            byte[] contractCode = compilationResult.Compilation;

            var contractTxData = new ContractTxData(0, (RuntimeObserver.Gas)1, (RuntimeObserver.Gas)500_000, contractCode);
            var tx = new Transaction();
            tx.AddOutput(0, new Script(this.callDataSerializer.Serialize(contractTxData)));

            IContractTransactionContext transactionContext = new ContractTransactionContext(BlockHeight, CoinbaseAddress, MempoolFee, new uint160(2), tx);

            var executor = new ContractExecutor(
                this.callDataSerializer,
                this.state,
                this.refundProcessor,
                this.transferProcessor,
                this.stateFactory,
                this.stateProcessor,
                this.contractPrimitiveSerializer);

            IContractExecutionResult result = executor.Execute(transactionContext);

            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public void CanWhitelistSystemContracts()
        {
            var internalTxExecutor = new Mock<IInternalTransactionExecutor>();
            var internalHashHelper = new Mock<IInternalHashHelper>();
            var persistentState = new TestPersistentState();
            var block = new Mock<IBlock>();
            var message = new Mock<IMessage>();
            Func<ulong> getBalance = () => 1;

            ISmartContractState state = Mock.Of<ISmartContractState>(
                g => g.InternalTransactionExecutor == internalTxExecutor.Object
                     && g.InternalHashHelper == internalHashHelper.Object
                     && g.PersistentState == persistentState
                     && g.Block == block.Object
                     && g.Message == message.Object
                     && g.GetBalance == getBalance);

            IContract contract = Contract.CreateUninitialized(typeof(SystemContractsDictionary), state, new uint160(2));
            var instance = (SystemContractsDictionary)contract.GetPrivateFieldValue("instance");

            // TODO: Verify signatures.
            byte[] signatures = new byte[] { 0 };
            UInt256 codeHash = 1;
            string name = "Name";
            Address address = new Address(0, 0, 0, 0, 1);

            contract.InvokeConstructor(new object[] { });

            var callGetSignatories = new MethodCall("GetSignatories", new object[] { "main" });
            IContractInvocationResult resultGetSignatories = contract.Invoke(callGetSignatories);
            Assert.True(resultGetSignatories.IsSuccess);
            Assert.Equal(3, ((Address[])resultGetSignatories.Return).Length);

            var callAddSignatory = new MethodCall("AddSignatory", new object[] { "main", new Address(0, 0, 0, 0, 3), (uint)4, (uint)3 });
            IContractInvocationResult resultAddSignatory = contract.Invoke(callAddSignatory);
            Assert.True(resultAddSignatory.IsSuccess);

            var callGetSignatories2 = new MethodCall("GetSignatories", new object[] { "main" });
            IContractInvocationResult resultGetSignatories2 = contract.Invoke(callGetSignatories2);
            Assert.True(resultGetSignatories2.IsSuccess);
            Assert.Equal(4, ((Address[])resultGetSignatories2.Return).Length);

            var callGetQuorum = new MethodCall("GetQuorum", new object[] { "main" });
            IContractInvocationResult resultGetQuorum = contract.Invoke(callGetQuorum);
            Assert.True(resultGetQuorum.IsSuccess);
            Assert.Equal((uint)3, (uint)resultGetQuorum.Return);

            var callRemoveSignatory = new MethodCall("RemoveSignatory", new object[] { "main", new Address(0, 0, 0, 0, 2), (uint)3, (uint)2 });
            IContractInvocationResult resultRemoveSignatory = contract.Invoke(callRemoveSignatory);
            Assert.True(resultRemoveSignatory.IsSuccess);

            var callGetSignatories3 = new MethodCall("GetSignatories", new object[] { "main" });
            IContractInvocationResult resultGetSignatories3 = contract.Invoke(callGetSignatories3);
            Assert.True(resultGetSignatories3.IsSuccess);
            Assert.Equal(3, ((Address[])resultGetSignatories3.Return).Length);

            var callGetQuorum2 = new MethodCall("GetQuorum", new object[] { "main" });
            IContractInvocationResult resultGetQuorum2 = contract.Invoke(callGetQuorum2);
            Assert.True(resultGetQuorum2.IsSuccess);
            Assert.Equal((uint)2, (uint)resultGetQuorum2.Return);

            var callWhiteList = new MethodCall("WhiteList", new object[] { signatures, codeHash, address, name });
            IContractInvocationResult resultWhiteList = contract.Invoke(callWhiteList);
            Assert.True(resultWhiteList.IsSuccess);

            WhiteListEntry whiteListEntry = persistentState.GetStruct<WhiteListEntry>(codeHash.ToString());

            Assert.Equal(name, whiteListEntry.Name);
            Assert.Equal(codeHash, whiteListEntry.CodeHash);
            Assert.Equal(address, whiteListEntry.LastAddress);

            Assert.Equal(codeHash, persistentState.GetUInt256($"ByName:{name}"));

            var callIsWhiteListed = new MethodCall("IsWhiteListed", new object[] { codeHash });
            IContractInvocationResult resultIsWhiteListed = contract.Invoke(callIsWhiteListed);
            Assert.True((bool)resultIsWhiteListed.Return);

            var callGetCodeHash = new MethodCall("GetCodeHash", new object[] { name });
            IContractInvocationResult resultGetCodeHash = contract.Invoke(callGetCodeHash);
            Assert.Equal(codeHash, (UInt256)resultGetCodeHash.Return);

            var callGetContractAddress = new MethodCall("GetContractAddress", new object[] { name });
            IContractInvocationResult resultGetContractAddress = contract.Invoke(callGetContractAddress);
            Assert.Equal(address, (Address)resultGetContractAddress.Return);

            var callGetContractAddressCH = new MethodCall("GetContractAddress", new object[] { codeHash });
            IContractInvocationResult resultGetContractAddressCH = contract.Invoke(callGetContractAddressCH);
            Assert.Equal(address, (Address)resultGetContractAddressCH.Return);

            var callBlackList = new MethodCall("BlackList", new object[] { signatures, codeHash });
            IContractInvocationResult resultBlackList = contract.Invoke(callBlackList);
            Assert.True(resultBlackList.IsSuccess);

            var callIsWhiteListed2 = new MethodCall("IsWhiteListed", new object[] { codeHash });
            IContractInvocationResult resultIsWhiteListed2 = contract.Invoke(callIsWhiteListed2);
            Assert.False((bool)resultIsWhiteListed2.Return);

            // These methods don't return anything once the conteact is black-listed.

            Assert.Equal(default(UInt256), persistentState.GetUInt256($"ByName:{name}"));

            var callGetCodeHash2 = new MethodCall("GetCodeHash", new object[] { name });
            IContractInvocationResult resultGetCodeHash2 = contract.Invoke(callGetCodeHash2);
            Assert.Equal(default(UInt256), (UInt256)resultGetCodeHash2.Return);

            var callGetContractAddress2 = new MethodCall("GetContractAddress", new object[] { name });
            IContractInvocationResult resultGetContractAddress2 = contract.Invoke(callGetContractAddress2);
            Assert.Equal(default(Address), (Address)resultGetContractAddress2.Return);

            var callGetContractAddressCH2 = new MethodCall("GetContractAddress", new object[] { codeHash });
            IContractInvocationResult resultGetContractAddressCH2 = contract.Invoke(callGetContractAddressCH2);
            Assert.Equal(default(Address), (Address)resultGetContractAddressCH2.Return);
        }
    }
}