-- Run in the existing FamilyRate Supabase project before deploying romanceiq-story.
-- The shared public.story_nodes table and existing private dynamic-function are reused.
-- Distinct ri-v1 hashed path keys keep the titles separate.
create table if not exists public.romanceiq_generation_budget (
  utc_day date primary key,
  requested_count integer not null default 0 check (requested_count >= 0)
);
alter table public.romanceiq_generation_budget enable row level security;
revoke all on public.romanceiq_generation_budget from anon, authenticated;
create or replace function public.romanceiq_consume_generation_budget()
returns boolean language plpgsql security definer set search_path = '' as $$
begin
  insert into public.romanceiq_generation_budget(utc_day,requested_count)
  values((now() at time zone 'UTC')::date,1)
  on conflict (utc_day) do update
    set requested_count=public.romanceiq_generation_budget.requested_count+1
    where public.romanceiq_generation_budget.requested_count<200;
  return found;
end;
$$;
revoke all on function public.romanceiq_consume_generation_budget() from PUBLIC, anon, authenticated;
grant execute on function public.romanceiq_consume_generation_budget() to service_role;
