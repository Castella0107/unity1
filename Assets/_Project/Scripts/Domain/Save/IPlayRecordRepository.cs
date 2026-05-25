using System.Collections.Generic;
using System.Threading.Tasks;

// Unity-independent. No UnityEngine references allowed in this assembly.
/// <summary>
/// プレイ記録の保存・取得およびパーソナルベストの管理を行うリポジトリの抽象インターフェース。
/// </summary>
public interface IPlayRecordRepository
{
    /// <summary>指定パスのデータストアを初期化する。</summary>
    Task InitializeAsync(string dbPath);

    /// <summary>プレイ記録を保存し、必要に応じてパーソナルベストを更新する。</summary>
    Task<bool>         SaveAsync(PlayRecord record);
    /// <summary>プレイIDで記録を取得する(無ければ null)。</summary>
    Task<PlayRecord>   GetByIdAsync(string playId);

    /// <summary>指定楽曲・難易度のパーソナルベストを取得する。</summary>
    Task<PersonalBest>       GetBestAsync(string songId, string difficulty);
    /// <summary>全てのパーソナルベストを取得する。</summary>
    Task<List<PersonalBest>> GetAllBestsAsync();

    /// <summary>指定楽曲・難易度のプレイ履歴を新しい順に取得する。</summary>
    Task<List<PlayRecord>> GetHistoryAsync(string songId, string difficulty, int limit = 50);
    /// <summary>全プレイ履歴をページング取得する。</summary>
    Task<List<PlayRecord>> GetAllHistoryAsync(int limit = 50, int offset = 0);
    /// <summary>総プレイ回数を取得する。</summary>
    Task<int>              GetTotalPlaysAsync();

    /// <summary>全プレイ記録を削除する。</summary>
    Task<bool> DeleteAllAsync();
}
