using System.Collections.Generic;
using System.Threading.Tasks;

// Unity-independent. No UnityEngine references allowed in this assembly.
/// <summary>
/// プレイ記録の保存・取得およびパーソナルベストの管理を行うリポジトリの抽象インターフェース。
/// </summary>
public interface IPlayRecordRepository
{
    Task InitializeAsync(string dbPath);

    Task<bool>         SaveAsync(PlayRecord record);
    Task<PlayRecord>   GetByIdAsync(string playId);

    Task<PersonalBest>       GetBestAsync(string songId, string difficulty);
    Task<List<PersonalBest>> GetAllBestsAsync();

    Task<List<PlayRecord>> GetHistoryAsync(string songId, string difficulty, int limit = 50);
    Task<List<PlayRecord>> GetAllHistoryAsync(int limit = 50, int offset = 0);
    Task<int>              GetTotalPlaysAsync();

    Task<bool> DeleteAllAsync();
}
