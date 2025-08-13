# Data Schema Documentation

## SPY 1-Minute Bars (`data/spy_1m/YYYY-MM-DD.parquet`)

| Column | Type | Description |
|--------|------|-------------|
| timestamp | datetime64[ns, UTC] | Minute timestamp (RTH: 14:30-21:00 UTC) |
| open | float64 | Open price |
| high | float64 | High price |
| low | float64 | Low price |
| close | float64 | Close price |
| volume | int64 | Share volume |

## VIX Daily (`data/vix/vix_daily.parquet`)

| Column | Type | Description |
|--------|------|-------------|
| date | date | Trading date |
| close | float64 | VIX closing level |

## VIX9D Daily (`data/vix9d/vix9d_daily.parquet`)

| Column | Type | Description |
|--------|------|-------------|
| date | date | Trading date |
| close | float64 | VIX9D closing level |

## Economic Calendar (`data/calendar/calendar.csv`)

| Column | Type | Description |
|--------|------|-------------|
| datetime_utc | datetime | Event time in UTC |
| event_name | str | CPI, FOMC, NFP, etc. |
| importance | str | high, medium, low |
| actual | float | Actual value (if numeric) |
| forecast | float | Forecast value (if numeric) |

## Features (`features/YYYY-MM-DD.parquet`)

| Column | Type | Description |
|--------|------|-------------|
| timestamp | datetime64[ns, UTC] | Minute timestamp |
| close | float64 | SPY close price |
| or_high | float64 | Opening Range (15m) high |
| or_low | float64 | Opening Range (15m) low |
| or_range | float64 | OR high - OR low |
| vwap_30m | float64 | 30-minute VWAP |
| atr_20 | float64 | 20-period ATR |
| momentum_5m | float64 | 5-minute price momentum |
| momentum_15m | float64 | 15-minute price momentum |
| vix | float64 | VIX level (daily, forward-filled) |
| vix9d | float64 | VIX9D level (daily, forward-filled) |
| session_pct | float64 | Session completion (0.0 - 1.0) |

## Archetypes (`data/archetypes/labels.csv`)

| Column | Type | Description |
|--------|------|-------------|
| date | date | Trading date |
| archetype | str | calm_range, trend_up, trend_dn, fakeout, event_spike_fade |
| or_range_pct | float64 | OR range as % of ATR |
| vwap_side_pct | float64 | % of session above VWAP |
| range_atr_ratio | float64 | Daily range / ATR ratio |