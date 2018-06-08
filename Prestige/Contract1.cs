using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.ComponentModel;
using System.Numerics;

namespace ICO_Template
{
    public class ICO_Template : SmartContract
    {
        //Token Settings
        public static string Name() => "Prestige Token";
        public static string Symbol() => "PRE";
        private static readonly byte[] owner = "AWj6LSC3TPbC5DXBrdsU4CTNRt9iSXmVpe".ToScriptHash();
        public static byte Decimals() => 8;
        private const ulong factor = 100000000; //decided by Decimals()
        
        //ICO Settings
        private static readonly byte[] neo_asset_id = { 155, 124, 255, 218, 166, 116, 190, 174, 15, 147, 14, 190, 96, 133, 175, 144, 147, 229, 254, 86, 179, 74, 92, 34, 12, 205, 207, 110, 252, 51, 111, 197 };
        private static readonly byte[] gas_asset_id = { 231, 45, 40, 105, 121, 238, 108, 177, 183, 230, 93, 253, 223, 178, 227, 132, 16, 11, 141, 20, 142, 119, 88, 222, 66, 228, 22, 139, 113, 121, 44, 96 };
        private const ulong total_amount = 1000000000 * factor; // total token amount
        private const ulong total_amount_per_stage = 125000000 * factor; // total token amount per stage
        private const ulong pre_ico_cap = 625000000 * factor; // pre ico token amount
        private const ulong basic_rate = 1000 * factor;
        private const int ico_start_time = 1525478400; //5/5/2018 00:00:00
        private const ulong neo_price = 7652; // CAUTION: PUT neoprice here mutiplied by 100
        private const ulong gas_price = 3173; // CAUTION: PUT gasprice here mutiplied by 100

        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> OnTransfer;

        [DisplayName("refund")]
        public static event Action<byte[], BigInteger> OnRefund;

        [DisplayName("approve")]
        public static event Action<byte[], byte[], BigInteger> OnApproved;

        public static Object Main(string operation, params object[] args)
        {
            if (Runtime.Trigger == TriggerType.Verification)
            {
                // if const param Owner is script hash
                bool isOwner = Runtime.CheckWitness(owner);
                if (isOwner) return true;

                ulong neo_sent_value = GetNeoContributeValue();
                ulong gas_sent_value = GetGasContributeValue();
                if (neo_sent_value > 0 || gas_sent_value > 0) return true;
                return false;
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                if (operation == "deploy") return Deploy();
                if (operation == "mintTokens") return MintTokens();
                if (operation == "totalSupply") return TotalSupply();
                if (operation == "name") return Name();
                if (operation == "symbol") return Symbol();
                if (operation == "transfer")
                {
                    if (args.Length != 3) return false;
                    byte[] from = (byte[])args[0];
                    byte[] to = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];
                    return Transfer(from, to, value);
                }
                if (operation == "balanceOf")
                {
                    if (args.Length != 1) return 0;
                    byte[] account = (byte[])args[0];
                    return BalanceOf(account);
                }
                if (operation == "decimals") return Decimals();
            }
            //you can choice refund or not refund
            byte[] sender = GetSender();
            ulong neo_contribute_value = GetNeoContributeValue();
            ulong gas_contribute_value = GetGasContributeValue();
            if ((neo_contribute_value > 0 || neo_contribute_value > 0) && sender.Length != 0)
            {
                if (neo_contribute_value > 0) {
                    OnRefund(sender, neo_contribute_value);
                }
                if (neo_contribute_value > 0)
                {
                    OnRefund(sender, neo_contribute_value);
                }
            }
            return false;
        }

        // initialization parameters, only once
        public static bool Deploy()
        {
            byte[] total_supply = Storage.Get(Storage.CurrentContext, "totalSupply");
            if (total_supply.Length != 0) return false;
            Storage.Put(Storage.CurrentContext, owner, pre_ico_cap);
            Storage.Put(Storage.CurrentContext, "totalSupply", pre_ico_cap);
            OnTransfer(null, owner, pre_ico_cap);
            return true;
        }

        // The function MintTokens is only usable by the chosen wallet
        // contract to mint a number of tokens proportional to the
        // amount of neo sent to the wallet contract. The function
        // can only be called during the tokenswap period
        public static bool MintTokens()
        {
            byte[] sender = GetSender();
            // contribute asset is not neo or gas
            if (sender.Length == 0)
            {
                return false;
            }

            ulong neo_contribute_value = GetNeoContributeValue();
            ulong gas_contribute_value = GetGasContributeValue();

            ulong token = 0;
            // you can get current swap token amount
            if (neo_contribute_value > 0)
            {
                token = CurrentNeoSwapToken(sender, neo_contribute_value);
            }
            else if (gas_contribute_value > 0)
            {
                token = CurrentGasSwapToken(sender, gas_contribute_value);
            }

            // refound neo
            if (token == 0 && neo_contribute_value > 0)
            {
                OnRefund(sender, neo_contribute_value);
                return false;
            }

            // refound gas
            if (token == 0 && gas_contribute_value > 0)
            {
                OnRefund(sender, gas_contribute_value);
                return false;
            }

            // crowdfunding success
            BigInteger balance = Storage.Get(Storage.CurrentContext, sender).AsBigInteger();
            Storage.Put(Storage.CurrentContext, sender, token + balance);
            BigInteger totalSupply = Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();
            Storage.Put(Storage.CurrentContext, "totalSupply", token + totalSupply);
            OnTransfer(null, sender, token);
            return true;
        }

        // get the total token supply
        public static BigInteger TotalSupply()
        {
            return Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();
        }

        // function that is always called when someone wants to transfer tokens.
        public static bool Transfer(byte[] from, byte[] to, BigInteger value)
        {
            if (value <= 0) return false;
            if (!Runtime.CheckWitness(from)) return false;
            BigInteger from_value = Storage.Get(Storage.CurrentContext, from).AsBigInteger();
            if (from_value < value) return false;
            if (from_value == value)
                Storage.Delete(Storage.CurrentContext, from);
            else
                Storage.Put(Storage.CurrentContext, from, from_value - value);
            BigInteger to_value = Storage.Get(Storage.CurrentContext, to).AsBigInteger();
            Storage.Put(Storage.CurrentContext, to, to_value + value);
            OnTransfer(from, to, value);
            return true;
        }

        // get the account balance of another account with address
        public static BigInteger BalanceOf(byte[] address)
        {
            return Storage.Get(Storage.CurrentContext, address).AsBigInteger();
        }

        // gets the real stage of selling tokens
        private static int CurrentStage()
        {
            uint now = Blockchain.GetHeader(Blockchain.GetHeight()).Timestamp + 15 * 60;
            int time = (int)now - ico_start_time;
            if (time < 0)
            {
                return 0;
            }
            else if (time < 172800) //2 days lasts the first round (2 * 24 * 60 * 60 ) 
            {
                return 1; // stage 1
            }
            else if (time < 345600) //4 days for the second round (4 * 24 * 60 * 60 ) 
            {
                return 2; // stage 2
            }
            else if (time < 518400) //6 days for the last round
            {
                return 3; // stage 3
            }
            else
            {
                return 0;
            }
        }

        // check that we're not over the total yet. 
        private static ulong CurrentNeoSwapToken(byte[] sender, ulong value)
        {
            // the current exchange rate between ico tokens and neo during the token swap period
            ulong swap_rate = 0;
            int current_stage = ICO_Template.CurrentStage();
            if (current_stage == 0)
            {
                swap_rate = 0;
            }
            else if (current_stage == 1)
            {
                swap_rate = 6; // Price on round 1 is 0.06 * 100 to avoid losing decimals
            }
            else if (current_stage == 2)
            {
                swap_rate = 7; // Price on round 2 is 0.07 * 100 to avoid losing decimals
            }
            else if (current_stage == 3)
            {
                swap_rate = 8; // Price on round 3 is 0.08 * 100 to avoid losing decimals
            }
            else
            {
                swap_rate = 0;
            }
            // crowdfunding failure
            if (swap_rate == 0)
            {
                OnRefund(sender, value);
                return 0;
            }
            ulong token = value * neo_price;
            token = token / swap_rate;
            BigInteger total_supply = Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();
            BigInteger balance_token = (pre_ico_cap + (total_amount_per_stage * (ulong)(ICO_Template.CurrentStage()))) - total_supply;
            if (balance_token <= 0)
            {
                OnRefund(sender, value);
                return 0;
            }
            return token;
        }

        // check that we're not over the total yet. 
        private static ulong CurrentGasSwapToken(byte[] sender, ulong value)
        {
            // the current exchange rate between ico tokens and neo during the token swap period
            ulong swap_rate = 0;
            int current_stage = ICO_Template.CurrentStage();
            if (current_stage == 0)
            {
                swap_rate = 0;
            }
            else if (current_stage == 1)
            {
                swap_rate = 6; // Price on round 1 is 0.06 * 100 to avoid losing decimals
            }
            else if (current_stage == 2)
            {
                swap_rate = 7; // Price on round 2 is 0.07 * 100 to avoid losing decimals
            }
            else if (current_stage == 3)
            {
                swap_rate = 8; // Price on round 3 is 0.08 * 100 to avoid losing decimals
            }
            else
            {
                swap_rate = 0;
            }
            // crowdfunding failure
            if (swap_rate == 0)
            {
                OnRefund(sender, value);
                return 0;
            }
            ulong token = value * gas_price;
            token = token / swap_rate;
            BigInteger total_supply = Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();
            BigInteger balance_token = (pre_ico_cap + (total_amount_per_stage * (ulong)(ICO_Template.CurrentStage()))) - total_supply;
            if (balance_token <= 0)
            {
                OnRefund(sender, value);
                return 0;
            }
            return token;
        }

        // check whether asset is neo and get sender script hash
        private static byte[] GetSender()
        {
            Transaction tx = (Transaction)ExecutionEngine.ScriptContainer;
            TransactionOutput[] reference = tx.GetReferences();
            // you can choice refund or not refund
            foreach (TransactionOutput output in reference)
            {
                if (output.AssetId == neo_asset_id || output.AssetId == gas_asset_id) return output.ScriptHash;
            }
            return new byte[0];
        }

        // get smart contract script hash
        private static byte[] GetReceiver()
        {
            return ExecutionEngine.ExecutingScriptHash;
        }

        // get all you contribute neo amount
        private static ulong GetNeoContributeValue()
        {
            Transaction tx = (Transaction)ExecutionEngine.ScriptContainer;
            TransactionOutput[] outputs = tx.GetOutputs();
            ulong value = 0;
            // get the total amount of Neo
            foreach (TransactionOutput output in outputs)
            {
                if (output.ScriptHash == GetReceiver() && output.AssetId == neo_asset_id)
                {
                    value += (ulong)output.Value;
                }
            }
            return value;
        }

        // get all you contribute gas amount
        private static ulong GetGasContributeValue()
        {
            Transaction tx = (Transaction)ExecutionEngine.ScriptContainer;
            TransactionOutput[] outputs = tx.GetOutputs();
            ulong value = 0;
            // get the total amount of Neo
            foreach (TransactionOutput output in outputs)
            {
                if (output.ScriptHash == GetReceiver() && output.AssetId == gas_asset_id)
                {
                    value += (ulong)output.Value;
                }
            }
            return value;
        }

        // Transfers tokens from the 'from' address to the 'to' address
        // The Sender must have an allowance from 'From' in order to send it to the 'To'
        // This matches the ERC20 version
        public static bool TransferFrom(byte[] sender, byte[] from, byte[] to, BigInteger value)
        {
            if (!Runtime.CheckWitness(sender)) return false;
            if (!ValidateAddress(from)) return false;
            if (!ValidateAddress(to)) return false;
            if (value <= 0) return false;

            BigInteger from_value = BalanceOf(from);
            if (from_value < value) return false;
            if (from == to) return true;

            // allowance of [from] to [sender]
            byte[] allowance_key = from.Concat(sender);
            BigInteger allowance = Storage.Get(Storage.CurrentContext, allowance_key).AsBigInteger();
            if (allowance < value) return false;

            if (from_value == value)
                Storage.Delete(Storage.CurrentContext, from);
            else
                Storage.Put(Storage.CurrentContext, from, from_value - value);

            if (allowance == value)
                Storage.Delete(Storage.CurrentContext, allowance_key);
            else
                Storage.Put(Storage.CurrentContext, allowance_key, allowance - value);

            // Sender sends tokens to 'To'
            BigInteger to_value = BalanceOf(to);
            Storage.Put(Storage.CurrentContext, to, to_value + value);

            OnTransfer(from, to, value);
            return true;
        }

        // Gives approval to the 'to' address to use amount of tokens from the 'from' address
        // This does not guarantee that the funds will be available later to be used by the 'to' address
        // 'From' is the Tx Sender. Each call overwrites the previous value. This matches the ERC20 version
        public static bool Approve(byte[] from, byte[] to, BigInteger value)
        {
            if (value <= 0) return false;
            if (!Runtime.CheckWitness(from)) return false;
            if (!ValidateAddress(to)) return false;
            if (from == to) return false;

            BigInteger from_value = BalanceOf(from);
            if (from_value < value) return false;

            // overwrite previous value
            byte[] allowance_key = from.Concat(to);
            Storage.Put(Storage.CurrentContext, allowance_key, value);
            OnApproved(from, to, value);
            return true;
        }

        // Gets the amount of tokens allowed by 'from' address to be used by 'to' address
        public static BigInteger Allowance(byte[] from, byte[] to)
        {
            if (!ValidateAddress(from)) return 0;
            if (!ValidateAddress(to)) return 0;
            byte[] allowance_key = from.Concat(to);
            return Storage.Get(Storage.CurrentContext, allowance_key).AsBigInteger();
        }

        public static bool ValidateAddress(byte[] address)
        {
            if (address.Length != 20)
                return false;
            if (address.AsBigInteger() == 0)
                return false;
            return true;
        }

    }
}