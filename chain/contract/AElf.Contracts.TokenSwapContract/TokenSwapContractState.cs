using AElf.Standards.ACS1;
using AElf.Contracts.MultiToken;
using AElf.Contracts.Parliament;
using AElf.Sdk.CSharp.State;
using AElf.Types;
using MTRecorder;
using Tokenswap;

namespace AElf.Contracts.TokenSwapContract
{
    public class TokenSwapContractState : ContractState
    {
        public MappedState<Hash, SwapInfo> SwapInfo { get; set; }
        public MappedState<Hash, SwapPair> SwapPairs { get; set; }
        public MappedState<Hash, Hash, SwapAmounts> Ledger { get; set; }
        public SingletonState<AuthorityInfo> MethodFeeController { get; set; }

        internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }

        internal MerkleTreeRecorderContractContainer.MerkleTreeRecorderContractReferenceState MerkleTreeRecorderContract
        {
            get;
            set;
        }

        internal ParliamentContractContainer.ParliamentContractReferenceState ParliamentContract { get; set; }
        public MappedState<string, MethodFees> TransactionFees { get; set; }
    }
}