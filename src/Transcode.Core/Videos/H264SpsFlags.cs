namespace Transcode.Core.Videos;

/*
Это факты о constraint-флагах, найденных в H.264 SPS codec extradata.
Они не описывают parameter sets, появляющиеся позже в потоке.
*/
/// <summary>
/// Describes constraint flags observed in H.264 SPS headers in codec extradata.
/// Each flag is true when it is set in at least one of those SPS headers.
/// This does not describe parameter sets introduced later in the stream.
/// </summary>
/// <param name="ConstraintSet4Flag">Установлен ли constraint_set4_flag хотя бы в одном проверенном SPS. / Whether any inspected SPS sets constraint_set4_flag.</param>
/// <param name="ConstraintSet5Flag">Установлен ли constraint_set5_flag хотя бы в одном проверенном SPS. / Whether any inspected SPS sets constraint_set5_flag.</param>
public sealed record H264SpsFlags(bool ConstraintSet4Flag, bool ConstraintSet5Flag);
