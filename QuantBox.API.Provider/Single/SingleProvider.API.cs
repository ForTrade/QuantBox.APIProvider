﻿using SmartQuant;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using XAPI.Callback;
using XAPI;
using NLog;
using System.Reflection;
using QuantBox.Extensions;
using System.IO;

namespace QuantBox.APIProvider.Single
{
    public partial class SingleProvider
    {
        static SingleProvider() {
            NLog.LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration(Path.Combine(Helper.RootPath.LocalPath, "NLog.config"), true);
        }
        //记录合约列表,从实盘合约名到对象的映射
        private readonly Dictionary<string, InstrumentField> _dictInstruments = new Dictionary<string, InstrumentField>();

        public static int GetDate(DateTime dt)
        {
            return dt.Year * 10000 + dt.Month * 100 + dt.Day;
        }

        public static int GetTime(DateTime dt)
        {
            return dt.Hour * 10000 + dt.Minute * 100 + dt.Second;
        }

        public static DateTime GetDateTime(int yyyyMMdd, int hhmmss, int Millisecond)
        {
            int yyyy = yyyyMMdd / 10000;
            int MM = yyyyMMdd % 10000 / 100;
            int dd = yyyyMMdd % 100;
            int hh = hhmmss / 10000;
            int mm = hhmmss % 10000 / 100;
            int ss = hhmmss % 100;
            DateTime dt = new DateTime(yyyy, MM, dd, hh, mm, ss, Millisecond);
            return dt;
        }

        private void OnRspQryInstrument_callback(object sender, ref InstrumentField instrument, int size1, bool bIsLast)
        {
            if (size1 <= 0)
            {
                (sender as XApi).Log.Info("OnRspQryInstrument");
                return;
            }

            _dictInstruments[instrument.Symbol] = instrument;
            
            if(bIsLast)
            {
                (sender as XApi).Log.Info("合约列表已经接收完成,共 {0} 条", _dictInstruments.Count);
            }
        }

        private void OnRspQryTradingAccount_callback(object sender, ref AccountField account, int size1, bool bIsLast)
        {
            if (size1 <= 0)
            {
                (sender as XApi).Log.Info("OnRspQryTradingAccount");
                return;
            }

            (sender as XApi).Log.Info("OnRspQryTradingAccount:" + account.ToFormattedString());

            if (!IsConnected)
                return;

            string currency = "CNY";

            AccountData ad = new AccountData(DateTime.Now, AccountDataType.AccountValue,
                account.AccountID, this.id, this.id);

            Type type = typeof(AccountField);
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in fields)
            {
                ad.Fields.Add(field.Name, currency, field.GetValue(account));
            }
            // 将对像完全设置进去，等着取出
            ad.Fields.Add(AccountDataFieldEx.USER_DATA, currency, account);
            ad.Fields.Add(AccountDataFieldEx.DATE, currency, GetDate(DateTime.Today));


            try
            {
                EmitAccountData(ad);
            }
            catch (Exception ex)
            {
                (sender as XApi).Log.Error(ex);
            }
        }

        private void OnRspQryInvestor_callback(object sender, ref InvestorField investor, int size1, bool bIsLast)
        {
            if (size1 <= 0)
            {
                (sender as XApi).Log.Info("OnRspQryInvestor");
                return;
            }

            (sender as XApi).Log.Info("OnRspQryInvestor:{0}", investor.ToFormattedString());
        }

        private void OnRspQryInvestorPosition_callback(object sender, ref PositionField position, int size1, bool bIsLast)
        {
            if (size1 <= 0)
            {
                (sender as XApi).Log.Info("OnRspQryInvestorPosition");
                return;
            }

            (sender as XApi).Log.Info("OnRspQryInvestorPosition:" + position.ToFormattedString());

            if (!IsConnected)
                return;

            PositionFieldEx item;
            if (!positions.TryGetValue(position.Symbol, out item))
            {
                item = new PositionFieldEx();
                positions[position.Symbol] = item;
            }
            item.AddPosition(position);

            AccountData ad = new AccountData(DateTime.Now, AccountDataType.Position,
                position.AccountID, this.id, this.id);

            ad.Fields.Add(AccountDataField.SYMBOL, item.Symbol);
            ad.Fields.Add(AccountDataField.EXCHANGE,item.Exchange);
            ad.Fields.Add(AccountDataField.QTY, item.Qty);
            ad.Fields.Add(AccountDataField.LONG_QTY, item.LongQty);
            ad.Fields.Add(AccountDataField.SHORT_QTY, item.ShortQty);

            ad.Fields.Add(AccountDataFieldEx.USER_DATA, item);
            ad.Fields.Add(AccountDataFieldEx.DATE, GetDate(DateTime.Today));

            ////ad.Fields.Add(AccountDataFieldEx.LONG_QTY_TD, item.Long.TdPosition);
            ////ad.Fields.Add(AccountDataFieldEx.LONG_QTY_YD, item.Long.YdPosition);
            ////ad.Fields.Add(AccountDataFieldEx.SHORT_QTY_TD, item.Short.TdPosition);
            ////ad.Fields.Add(AccountDataFieldEx.SHORT_QTY_YD, item.Short.YdPosition);

            try
            {
                EmitAccountData(ad);
            }
            catch (Exception ex)
            {
                (sender as XApi).Log.Error(ex);
            }
        }

        private void OnRspQrySettlementInfo(object sender, ref SettlementInfoClass settlementInfo, int size1, bool bIsLast)
        {
            if (size1 <= 0)
            {
                (sender as XApi).Log.Info("OnRspQrySettlementInfo");
                return;
            }

            if (bIsLast)
            {
                (sender as XApi).Log.Info("OnRspQrySettlementInfo:" + Environment.NewLine + settlementInfo.Content);
            }
        }


        private void OnRspQryQuote_callback(object sender, ref QuoteField quote, int size1, bool bIsLast)
        {
            if (size1 <= 0)
            {
                (sender as XApi).Log.Info("OnRspQryQuote");
                return;
            }

            (sender as XApi).Log.Info("OnRspQryQuote:" + quote.ToFormattedString());
        }

        private void OnRspQryTrade_callback(object sender, ref TradeField trade, int size1, bool bIsLast)
        {
            if (size1 <= 0)
            {
                (sender as XApi).Log.Info("OnRspQryTrade");
                return;
            }

            (sender as XApi).Log.Info("OnRspQryTrade:" + trade.ToFormattedString());
        }

        private void OnRspQryOrder_callback(object sender, ref OrderField order, int size1, bool bIsLast)
        {
            if (size1 <= 0)
            {
                (sender as XApi).Log.Info("OnRspQryOrder");
                return;
            }

            (sender as XApi).Log.Info("OnRspQryOrder:" + order.ToFormattedString());
        }
    }
}
