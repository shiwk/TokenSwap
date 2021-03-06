using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.ContractTestBase.ContractTestKit;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.SmartContract.Application;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.DependencyInjection;
using MTRecorder;
using Tokenswap;
using Volo.Abp.Threading;

namespace AElf.Contracts.TokenSwapContract
{
    public class TokenSwapContractTestBase : ContractTestBase<TokenSwapContractTestModule>
    {
        internal TokenContractImplContainer.TokenContractImplStub TokenContractStub { get; set; }
        protected ECKeyPair DefaultSenderKeyPair => SampleAccount.Accounts[0].KeyPair;
        protected ECKeyPair NormalKeyPair => SampleAccount.Accounts[1].KeyPair;
        protected Address DefaultSenderAddress => Address.FromPublicKey(DefaultSenderKeyPair.PublicKey);
        protected Address TokenSwapContractAddress => GetAddress(TokenSwapContractNameProvider.StringName);
        protected Address MerkleTreeRecorderContractAddress => GetAddress(MerkleTreeRecorderContractNameProvider.StringName);

        private IReadOnlyDictionary<string, byte[]> _patchedCodes;
        internal TokenSwapContractContainer.TokenSwapContractStub TokenSwapContractStub { get; set; }
        internal MerkleTreeRecorderContractContainer.MerkleTreeRecorderContractStub MerkleTreeRecorderContractStub { get; set; }


        public TokenSwapContractTestBase()
        {
            TokenContractStub = GetTokenContractStub(DefaultSenderKeyPair);
            TokenSwapContractStub = GetTokenSwapContractStub(DefaultSenderKeyPair);
            MerkleTreeRecorderContractStub = GetMerkleTreeRecorderContractStub(DefaultSenderKeyPair);
        }


        private Address GetAddress(string contractStringName)
        {
            var addressService = Application.ServiceProvider.GetRequiredService<ISmartContractAddressService>();
            var blockchainService = Application.ServiceProvider.GetRequiredService<IBlockchainService>();
            var chain = AsyncHelper.RunSync(blockchainService.GetChainAsync);
            var address = AsyncHelper.RunSync(() => addressService.GetSmartContractAddressAsync(new ChainContext
            {
                BlockHash = chain.BestChainHash,
                BlockHeight = chain.BestChainHeight
            }, contractStringName)).SmartContractAddress.Address;
            return address;
        }

        protected async Task CreateAndApproveTokenAsync(string tokenName, string symbol, int decimals, long totalSupply,
            long approveAmount)
        {
            var createInput = new CreateInput
            {
                Symbol = symbol,
                TokenName = tokenName,
                IsBurnable = true,
                Decimals = decimals,
                Issuer = DefaultSenderAddress,
                TotalSupply = totalSupply
            };
            await TokenContractStub.Create.SendAsync(createInput);

            var issueInput = new IssueInput
            {
                Amount = totalSupply,
                Symbol = symbol,
                To = DefaultSenderAddress
            };
            await TokenContractStub.Issue.SendAsync(issueInput);

            var approveInput = new ApproveInput
            {
                Amount = approveAmount,
                Spender = TokenSwapContractAddress,
                Symbol = symbol
            };
            await TokenContractStub.Approve.SendAsync(approveInput);
        }

        private async Task ApproveTokenAsync(string symbol, long approveAmount)
        {
            var approveInput = new ApproveInput
            {
                Amount = approveAmount,
                Spender = TokenSwapContractAddress,
                Symbol = symbol
            };
            await TokenContractStub.Approve.SendAsync(approveInput);
        }

        internal async Task<Hash> CreateSwapAsync(string symbol = "ELF", int originTokenSizeInByte = 32,
            SwapRatio ratio = null, long depositAmount = 0, bool isBigEndian = true)
        {
            var swapRatio = ratio ?? new SwapRatio
            {
                OriginShare = 10_000_000_000, //1e18
                TargetShare = 1 // 1e8
            };
            var addSwapPairTx = await TokenSwapContractStub.CreateSwap.SendAsync(new CreateSwapInput()
            {
                OriginTokenSizeInByte = originTokenSizeInByte,

                OriginTokenNumericBigEndian = isBigEndian,
                SwapTargetTokenList =
                {
                    new SwapTargetToken
                    {
                        SwapRatio = swapRatio,
                        TargetTokenSymbol = symbol,
                        DepositAmount = depositAmount == 0 ? TotalSupply / 2 : depositAmount,
                    }
                }
            });
            var swapId = addSwapPairTx.Output;
            return swapId;
        }

        internal async Task<Hash> CreateSwapWithMultiTargetTokenAsync(int originTokenSizeInByte = 32,
            bool isBigEndian = true, params SwapTargetToken[] swapTargetTokens)
        {
            var addSwapPairTx = await TokenSwapContractStub.CreateSwap.SendAsync(new CreateSwapInput()
            {
                OriginTokenSizeInByte = originTokenSizeInByte,

                OriginTokenNumericBigEndian = isBigEndian,
                SwapTargetTokenList =
                {
                    swapTargetTokens
                }
            });
            var swapId = addSwapPairTx.Output;
            return swapId;
        }

        protected async Task InitializeAsync()
        {
            await MerkleTreeRecorderContractStub.Initialize.SendAsync(new Empty());
            await MerkleTreeRecorderContractStub.CreateRecorder.SendAsync(new Recorder
            {
                Admin = DefaultAccount.Address,
                MaximalLeafCount = 1024
            });
            
            await TokenSwapContractStub.Initialize.SendAsync(new InitializeInput
            {
                MerkleTreeRecorderAddress = GetAddress(MerkleTreeRecorderContractNameProvider.StringName)
            });
        }

        protected async Task RecordeMerkleTree(Hash merkleTreeRoot, long lastLeafIndex)
        {
            await MerkleTreeRecorderContractStub.RecordMerkleTree.SendAsync(new RecordMerkleTreeInput
            {
                LastLeafIndex = lastLeafIndex,
                MerkleTreeRoot = merkleTreeRoot,
                RecorderId = 0
            });
        }

        internal TokenSwapContractContainer.TokenSwapContractStub GetTokenSwapContractStub(ECKeyPair ecKeyPair)
        {
            return GetTester<TokenSwapContractContainer.TokenSwapContractStub>(TokenSwapContractAddress, ecKeyPair);
        }

        internal MerkleTreeRecorderContractContainer.MerkleTreeRecorderContractStub GetMerkleTreeRecorderContractStub(ECKeyPair ecKeyPair)
        {
            return GetTester<MerkleTreeRecorderContractContainer.MerkleTreeRecorderContractStub>(MerkleTreeRecorderContractAddress, ecKeyPair);
        }
        
        internal TokenContractImplContainer.TokenContractImplStub GetTokenContractStub(ECKeyPair senderKeyPair)
        {
            return GetTester<TokenContractImplContainer.TokenContractImplStub>(TokenContractAddress, senderKeyPair);
        }
        
        protected Hash GetHashTokenAmountData(decimal amount, int originTokenSizeInByte)
        {
            var preHolderSize = originTokenSizeInByte - 16;
            var amountInIntegers = decimal.GetBits(amount).Reverse().ToArray();

            if (preHolderSize < 0)
                amountInIntegers = amountInIntegers.TakeLast(originTokenSizeInByte / 4).ToArray();

            var amountBytes = new List<byte>();
            amountInIntegers.Aggregate(amountBytes, (cur, i) =>
            {
                while (cur.Count < preHolderSize)
                {
                    cur.Add(new byte());
                }

                cur.AddRange(i.ToBytes());
                return cur;
            });
            return HashHelper.ComputeFrom(amountBytes.ToArray());
        }

        protected bool TryGetOriginTokenAmount(string amountInString, out decimal amount)
        {
            return decimal.TryParse(amountInString, out amount);
        }

        protected async Task CreatAndIssueDefaultTokenAsync()
        {
            await ApproveTokenAsync(DefaultSymbol1, TotalSupply);
            await CreateAndApproveTokenAsync(TokenName2, DefaultSymbol2, 8, TotalSupply * 10, TotalSupply * 10);
        }

        protected string DefaultSymbol1 { get; set; } = "ELF";
        protected string DefaultSymbol2 { get; set; } = "ABC";

        protected string TokenName2 { get; set; } = "ABC";

        protected long TotalSupply { get; set; } = 100_000_000_000_000_000;
    }
}