#include "StdAfx.h"
#include "IfBuilder.h"
#include <algorithm>

IfBuilder::IfBuilder(std::vector<std::pair<unsigned int, bool>> &blockStarts) : m_blockStarts(blockStarts)
{
}

IfBuilder::~IfBuilder(void)
{
}

void IfBuilder::AddBlockStart(int line, bool entered)
{
	m_blockStarts.push_back(std::make_pair(line, entered));
}
